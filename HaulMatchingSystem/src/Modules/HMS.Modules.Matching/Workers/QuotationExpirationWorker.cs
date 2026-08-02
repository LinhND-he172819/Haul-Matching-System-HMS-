using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Matching.Workers;

/// <summary>
/// Background service that expires quotations in Sent status past their ExpiresAt.
/// Runs every 2 minutes. When a quotation expires:
/// 1. Quotation status: Sent → Expired
/// 2. Shipment reverts to Approved state (customer can re-negotiate or cancel)
/// </summary>
public sealed class QuotationExpirationWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuotationExpirationWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    public QuotationExpirationWorker(IConfiguration configuration, IServiceProvider serviceProvider,
        ILogger<QuotationExpirationWorker> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QuotationExpirationWorker started. Interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                var expired = await ExpireQuotationsAsync(stoppingToken);
                if (expired > 0)
                {
                    _logger.LogInformation("Expired {Count} quotations.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in QuotationExpirationWorker");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("QuotationExpirationWorker stopped.");
    }

    private async Task<int> ExpireQuotationsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Find and expire quotations past their ExpiresAt
        const string expireSql = """
            UPDATE warehouse.quotations
            SET status = 'Expired',
                expired_at = NOW(),
                updated_at = NOW()
            WHERE status = 'Sent'
              AND expires_at < NOW()
              AND is_deleted = FALSE
            RETURNING id, proposal_id, quotation_code;
        """;

        await using var expireCmd = new NpgsqlCommand(expireSql, conn);
        await using var reader = await expireCmd.ExecuteReaderAsync(ct);

        var expiredQuotations = new List<(Guid Id, Guid ProposalId, string Code)>();
        while (await reader.ReadAsync(ct))
        {
            expiredQuotations.Add((
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2)
            ));
        }

        await reader.CloseAsync();

        if (expiredQuotations.Count == 0)
            return 0;

        // For each expired quotation, we need to:
        // 1. Revert shipment PendingDeposit → PendingReview (Part 4)
        // 2. Insert audit log
        // 3. Notify the customer
        // 4. SignalR quotation expired event
        foreach (var (quotationId, proposalId, code) in expiredQuotations)
        {
            try
            {
                // 1. Revert shipment status if it's in PendingDeposit
                const string revertSql = """
                    UPDATE warehouse.shipments
                    SET status = 'PendingReview', updated_at = NOW()
                    WHERE id = (
                        SELECT shipment_id FROM warehouse.shipment_proposals
                        WHERE id = @proposal_id AND is_deleted = FALSE
                    )
                    AND status = 'PendingDeposit'
                    AND is_deleted = FALSE;
                """;
                await using var revertCmd = new NpgsqlCommand(revertSql, conn);
                revertCmd.Parameters.AddWithValue("proposal_id", proposalId);
                await revertCmd.ExecuteNonQueryAsync(ct);

                // 2. Audit
                const string auditSql = """
                    INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                    VALUES ('Quotation', @quotation_id, 'Expired', NULL, @details::jsonb, NOW());
                """;
                await using var auditCmd = new NpgsqlCommand(auditSql, conn);
                auditCmd.Parameters.AddWithValue("quotation_id", quotationId);
                auditCmd.Parameters.AddWithValue("details",
                    $"{{\"message\": \"Quotation {code} expired automatically. Shipment reverted to PendingReview.\"}}");
                await auditCmd.ExecuteNonQueryAsync(ct);

                // 3. Get customer for notification
                const string notifSql = """
                    SELECT sp.customer_id
                    FROM warehouse.shipment_proposals sp
                    WHERE sp.id = @proposal_id AND sp.is_deleted = FALSE;
                """;
                await using var notifCmd = new NpgsqlCommand(notifSql, conn);
                notifCmd.Parameters.AddWithValue("proposal_id", proposalId);
                var customerId = await notifCmd.ExecuteScalarAsync(ct) as Guid?;

                if (customerId.HasValue)
                {
                    // 3a. Database notification
                    const string insertNotifSql = """
                        INSERT INTO shared.notifications (user_id, title, message, entity_type, entity_id, created_at)
                        VALUES (@user_id, @title, @message, 'Quotation', @quotation_id, NOW());
                    """;
                    await using var notifCmd2 = new NpgsqlCommand(insertNotifSql, conn);
                    notifCmd2.Parameters.AddWithValue("user_id", customerId.Value);
                    notifCmd2.Parameters.AddWithValue("title", "Báo giá đã hết hạn");
                    notifCmd2.Parameters.AddWithValue("message",
                        $"Báo giá #{code} đã hết hạn. Đơn hàng đã được chuyển về trạng thái chờ duyệt. Vui lòng liên hệ nhân viên để được hỗ trợ.");
                    notifCmd2.Parameters.AddWithValue("quotation_id", quotationId);
                    await notifCmd2.ExecuteNonQueryAsync(ct);
                }

                // 4. SignalR quotation expired event (via dispatcher in a scope)
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<HMS.Shared.Core.Interfaces.IRealtimeDispatcher>();
                    if (customerId.HasValue)
                    {
                        await dispatcher.SendQuotationToCustomerAsync(customerId.Value, new QuotationEventPayload
                        {
                            EventType = "QuotationExpired",
                            QuotationId = quotationId,
                            ProposalId = proposalId,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR for expired quotation {QuotationId}", quotationId);
                }

                _logger.LogInformation("Expired quotation {Code} (ID: {Id}), shipment reverted to PendingReview", code, quotationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired quotation {QuotationId}", quotationId);
            }
        }

        return expiredQuotations.Count;
    }
}
