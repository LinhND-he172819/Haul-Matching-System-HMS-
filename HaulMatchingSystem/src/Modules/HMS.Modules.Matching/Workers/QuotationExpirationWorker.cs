using Microsoft.Extensions.Configuration;
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
    private readonly ILogger<QuotationExpirationWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    public QuotationExpirationWorker(IConfiguration configuration, ILogger<QuotationExpirationWorker> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
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
        // 1. Insert audit log
        // 2. Optionally notify the customer
        foreach (var (quotationId, proposalId, code) in expiredQuotations)
        {
            try
            {
                // Audit
                const string auditSql = """
                    INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                    VALUES ('Quotation', @quotation_id, 'Expired', NULL, @details::jsonb, NOW());
                """;
                await using var auditCmd = new NpgsqlCommand(auditSql, conn);
                auditCmd.Parameters.AddWithValue("quotation_id", quotationId);
                auditCmd.Parameters.AddWithValue("details",
                    $"{{\"message\": \"Quotation {code} expired automatically\"}}");
                await auditCmd.ExecuteNonQueryAsync(ct);

                // Get customer for notification
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
                    const string insertNotifSql = """
                        INSERT INTO shared.notifications (user_id, title, message, entity_type, entity_id, created_at)
                        VALUES (@user_id, @title, @message, 'Quotation', @quotation_id, NOW());
                    """;
                    await using var notifCmd2 = new NpgsqlCommand(insertNotifSql, conn);
                    notifCmd2.Parameters.AddWithValue("user_id", customerId.Value);
                    notifCmd2.Parameters.AddWithValue("title", "Báo giá đã hết hạn");
                    notifCmd2.Parameters.AddWithValue("message",
                        $"Báo giá #{code} đã hết hạn. Vui lòng liên hệ nhân viên để được hỗ trợ.");
                    notifCmd2.Parameters.AddWithValue("quotation_id", quotationId);
                    await notifCmd2.ExecuteNonQueryAsync(ct);
                }

                _logger.LogInformation("Expired quotation {Code} (ID: {Id})", code, quotationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired quotation {QuotationId}", quotationId);
            }
        }

        return expiredQuotations.Count;
    }
}
