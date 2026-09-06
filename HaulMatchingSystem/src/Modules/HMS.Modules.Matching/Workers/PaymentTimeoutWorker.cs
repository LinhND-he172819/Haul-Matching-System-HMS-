using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Matching.Workers;

/// <summary>
/// Background service that auto-cancels pending payments past their timeout.
/// Runs every 5 minutes. When a payment is Pending and created_at > 30 min ago:
///   Pending → Cancelled
/// Does NOT change Shipment or Quotation status.
/// Sends Notification + Audit Log. SignalR is not dispatched here (no hub context in worker).
/// </summary>
public sealed class PaymentTimeoutWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly ILogger<PaymentTimeoutWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly int TimeoutMinutes = 30;

    public PaymentTimeoutWorker(IConfiguration configuration, ILogger<PaymentTimeoutWorker> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentTimeoutWorker started. Interval: {Interval}, Timeout: {Timeout}min", Interval, TimeoutMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                var cancelled = await CancelTimedOutPaymentsAsync(stoppingToken);
                if (cancelled > 0)
                {
                    _logger.LogInformation("Auto-cancelled {Count} timed-out payments.", cancelled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PaymentTimeoutWorker");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("PaymentTimeoutWorker stopped.");
    }

    private async Task<int> CancelTimedOutPaymentsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Find pending payments older than timeout threshold
        const string findSqlParam = """
            UPDATE warehouse.payments
            SET status = 'Cancelled',
                updated_at = NOW()
            WHERE status = 'Pending'
              AND created_at < NOW() - (@timeout || ' minutes')::interval
              AND is_deleted = FALSE
            RETURNING id, payment_code, payment_type, amount, currency, quotation_id, shipment_id, customer_id;
        """;

        await using var cmd = new NpgsqlCommand(findSqlParam, conn);
        cmd.Parameters.AddWithValue("timeout", TimeoutMinutes.ToString());
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var timedOutPayments = new List<(Guid Id, string Code, string Type, decimal Amount,
            string Currency, Guid? QuotationId, Guid ShipmentId, Guid CustomerId)>();

        while (await reader.ReadAsync(ct))
        {
            timedOutPayments.Add((
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5),
                reader.GetGuid(6),
                reader.GetGuid(7)
            ));
        }

        await reader.CloseAsync();

        if (timedOutPayments.Count == 0)
            return 0;

        foreach (var (paymentId, code, type, amount, currency, quotationId, shipmentId, customerId) in timedOutPayments)
        {
            try
            {
                // Audit log
                const string auditSql = """
                    INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                    VALUES ('Payment', @payment_id, 'Timeout', NULL, @details::jsonb, NOW());
                """;
                await using var auditCmd = new NpgsqlCommand(auditSql, conn);
                auditCmd.Parameters.AddWithValue("payment_id", paymentId);
                auditCmd.Parameters.AddWithValue("details",
                    $"{{\"message\": \"Payment {code} auto-cancelled due to timeout ({TimeoutMinutes}min)\"}}");
                await auditCmd.ExecuteNonQueryAsync(ct);

                // Notification to customer
                var title = type == "Deposit"
                    ? "Deposit Timeout"
                    : "Final Payment Timeout";
                var message = type == "Deposit"
                    ? $"Thanh toán cọc {code} ({amount} {currency}) đã hết hạn và tự động hủy sau {TimeoutMinutes} phút."
                    : $"Thanh toán cuối {code} ({amount} {currency}) đã hết hạn và tự động hủy sau {TimeoutMinutes} phút.";

                const string notifSql = """
                    INSERT INTO shared.notifications (user_id, title, message, entity_type, entity_id, created_at)
                    VALUES (@user_id, @title, @message, 'Payment', @payment_id, NOW());
                """;
                await using var notifCmd = new NpgsqlCommand(notifSql, conn);
                notifCmd.Parameters.AddWithValue("user_id", customerId);
                notifCmd.Parameters.AddWithValue("title", title);
                notifCmd.Parameters.AddWithValue("message", message);
                notifCmd.Parameters.AddWithValue("payment_id", paymentId);
                await notifCmd.ExecuteNonQueryAsync(ct);

                _logger.LogInformation("Auto-cancelled timed-out payment {Code} (ID: {Id})", code, paymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timed-out payment {PaymentId}", paymentId);
            }
        }

        return timedOutPayments.Count;
    }
}
