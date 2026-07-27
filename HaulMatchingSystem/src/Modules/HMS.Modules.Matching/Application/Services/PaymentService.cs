using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace HMS.Modules.Matching.Application.Services
{
    /// <summary>
    /// Payment service using raw Npgsql with transaction support.
    /// Handles deposit payment, final payment, webhook processing.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly string _connStr;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IConfiguration configuration,
            IShipmentStateService shipmentStateService,
            IRealtimeDispatcher dispatcher,
            ILogger<PaymentService> logger)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
            _shipmentStateService = shipmentStateService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        /// <summary>
        /// Create a deposit payment for a quotation.
        /// Quotation must be in Sent status; Shipment must be in PendingDeposit.
        /// </summary>
        public async Task<PaymentResponseDto> CreateDepositPaymentAsync(
            Guid quotationId, Guid customerId, CreateDepositPaymentRequest request,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // 1. Read quotation
                var quotation = await ReadQuotationForUpdateAsync(conn, tx, quotationId, ct)
                    ?? throw new InvalidOperationException("Báo giá không tồn tại.");

                if (quotation.Status != "Sent")
                    throw new InvalidOperationException(
                        $"Chỉ có thể thanh toán cọc khi Báo giá ở trạng thái Sent. Hiện tại: {quotation.Status}");

                // 2. Check idempotency: no existing paid deposit for this quotation
                var existingDeposit = await GetPaidDepositForQuotationAsync(conn, tx, quotationId, ct);
                if (existingDeposit.HasValue)
                    throw new InvalidOperationException("Đã tồn tại khoản thanh toán cọc cho Báo giá này.");

                // 3. Read proposal → shipment
                var proposal = await ReadProposalAsync(conn, tx, quotation.ProposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                // 4. Validate shipment is PendingDeposit
                var shipment = await ReadShipmentForUpdateAsync(conn, tx, proposal.ShipmentId, ct)
                    ?? throw new InvalidOperationException("Shipment không tồn tại.");

                if (shipment.Status != ShipmentStatus.PendingDeposit.ToString())
                    throw new InvalidOperationException(
                        $"Shipment đang ở trạng thái {shipment.Status}. Cần PendingDeposit.");

                // 5. Check customer owns this proposal
                if (proposal.CustomerId != customerId)
                    throw new UnauthorizedAccessException("Không có quyền thanh toán cho Báo giá này.");

                // 6. Create payment record
                var paymentId = Guid.NewGuid();
                var paymentCode = await GeneratePaymentCodeAsync(conn, "Deposit", ct);
                var idempotencyKey = $"deposit-{quotationId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

                const string insertSql = """
                    INSERT INTO warehouse.payments
                        (id, payment_code, quotation_id, shipment_id, customer_id, payment_type,
                         amount, currency, payment_method, status, idempotency_key,
                         created_at, updated_at, is_deleted)
                    VALUES
                        (@id, @payment_code, @quotation_id, @shipment_id, @customer_id, 'Deposit',
                         @amount, @currency, @payment_method, 'Pending', @idempotency_key,
                         NOW(), NOW(), FALSE);
                """;
                await using (var cmd = new NpgsqlCommand(insertSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    cmd.Parameters.AddWithValue("payment_code", paymentCode);
                    cmd.Parameters.AddWithValue("quotation_id", quotationId);
                    cmd.Parameters.AddWithValue("shipment_id", proposal.ShipmentId);
                    cmd.Parameters.AddWithValue("customer_id", customerId);
                    cmd.Parameters.AddWithValue("amount", quotation.DepositAmount);
                    cmd.Parameters.AddWithValue("currency", quotation.Currency);
                    cmd.Parameters.AddWithValue("payment_method", (object)request.PaymentMethod! ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("idempotency_key", idempotencyKey);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 7. Audit
                await InsertAuditAsync(conn, tx, "Payment", paymentId, "Created",
                    $"Deposit payment created: {quotation.DepositAmount} {quotation.Currency}", customerId, ct);

                await tx.CommitAsync(ct);

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = paymentCode,
                    QuotationId = quotationId,
                    ShipmentId = proposal.ShipmentId,
                    PaymentType = "Deposit",
                    Amount = quotation.DepositAmount,
                    Currency = quotation.Currency,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Create a final payment (remaining amount) for a quotation.
        /// Deposit must be paid and shipment must be Delivered.
        /// </summary>
        public async Task<PaymentResponseDto> CreateFinalPaymentAsync(
            Guid quotationId, Guid customerId, CreateFinalPaymentRequest request,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // 1. Read quotation
                var quotation = await ReadQuotationForUpdateAsync(conn, tx, quotationId, ct)
                    ?? throw new InvalidOperationException("Báo giá không tồn tại.");

                if (quotation.Status != "Accepted")
                    throw new InvalidOperationException(
                        $"Chỉ có thể thanh toán cuối khi Báo giá đã chấp nhận. Hiện tại: {quotation.Status}");

                // 2. Read proposal → shipment
                var proposal = await ReadProposalAsync(conn, tx, quotation.ProposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                // 3. Validate shipment is Delivered
                var shipment = await ReadShipmentForUpdateAsync(conn, tx, proposal.ShipmentId, ct)
                    ?? throw new InvalidOperationException("Shipment không tồn tại.");

                if (shipment.Status != ShipmentStatus.Delivered.ToString())
                    throw new InvalidOperationException(
                        $"Shipment đang ở trạng thái {shipment.Status}. Cần Delivered để thanh toán cuối.");

                // 4. Check deposit was paid
                var depositPaid = await GetPaidDepositForQuotationAsync(conn, tx, quotationId, ct);
                if (!depositPaid.HasValue)
                    throw new InvalidOperationException("Cần thanh toán tiền cọc trước khi thanh toán phần còn lại.");

                // 5. Check customer owns this proposal
                if (proposal.CustomerId != customerId)
                    throw new UnauthorizedAccessException("Không có quyền thanh toán cho Báo giá này.");

                // 6. Calculate remaining amount
                var outstandingAmount = quotation.ShippingFee - quotation.DepositAmount;
                if (outstandingAmount <= 0)
                    throw new InvalidOperationException("Không có số tiền cần thanh toán thêm.");

                // 7. Create final payment record
                var paymentId = Guid.NewGuid();
                var paymentCode = await GeneratePaymentCodeAsync(conn, "Final", ct);
                var idempotencyKey = $"final-{quotationId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

                const string insertSql = """
                    INSERT INTO warehouse.payments
                        (id, payment_code, quotation_id, shipment_id, customer_id, payment_type,
                         amount, currency, payment_method, status, idempotency_key,
                         created_at, updated_at, is_deleted)
                    VALUES
                        (@id, @payment_code, @quotation_id, @shipment_id, @customer_id, 'FinalPayment',
                         @amount, @currency, @payment_method, 'Pending', @idempotency_key,
                         NOW(), NOW(), FALSE);
                """;
                await using (var cmd = new NpgsqlCommand(insertSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    cmd.Parameters.AddWithValue("payment_code", paymentCode);
                    cmd.Parameters.AddWithValue("quotation_id", quotationId);
                    cmd.Parameters.AddWithValue("shipment_id", proposal.ShipmentId);
                    cmd.Parameters.AddWithValue("customer_id", customerId);
                    cmd.Parameters.AddWithValue("amount", outstandingAmount);
                    cmd.Parameters.AddWithValue("currency", quotation.Currency);
                    cmd.Parameters.AddWithValue("payment_method", (object)request.PaymentMethod! ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("idempotency_key", idempotencyKey);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 8. Audit
                await InsertAuditAsync(conn, tx, "Payment", paymentId, "Created",
                    $"Final payment created: {outstandingAmount} {quotation.Currency}", customerId, ct);

                await tx.CommitAsync(ct);

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = paymentCode,
                    QuotationId = quotationId,
                    ShipmentId = proposal.ShipmentId,
                    PaymentType = "FinalPayment",
                    Amount = outstandingAmount,
                    Currency = quotation.Currency,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Process a payment webhook/callback.
        /// Handles both deposit and final payment acceptances with full state transitions.
        /// </summary>
        public async Task ProcessWebhookAsync(
            PaymentWebhookRequest webhook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(webhook.TransactionReference))
                throw new InvalidOperationException("TransactionReference là bắt buộc.");

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // 1. Find payment by transaction reference
                var paymentOpt = await ReadPaymentByTransactionRefAsync(conn, tx, webhook.TransactionReference, ct);
                if (paymentOpt == null)
                    throw new InvalidOperationException($"Không tìm thấy Payment với ref {webhook.TransactionReference}");
                var payment = paymentOpt.Value;

                // 2. Check if already processed
                if (payment.Status == "Paid")
                {
                    _logger.LogInformation("Payment {PaymentId} already processed as Paid", payment.Id);
                    await tx.RollbackAsync(ct);
                    return;
                }

                PaymentTransitionGuard.EnsureCanTransition(
                    Enum.Parse<PaymentStatus>(payment.Status),
                    webhook.Status == "Paid" ? PaymentStatus.Paid : PaymentStatus.Failed);

                // 3. Update payment status
                var newStatus = webhook.Status == "Paid" ? "Paid" : "Failed";
                var paidAt = webhook.Status == "Paid" ? (DateTime?)webhook.PaidAt : null;

                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = @status,
                        paid_at = @paid_at,
                        transaction_reference = @transaction_reference,
                        payment_method = COALESCE(@payment_method, payment_method),
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", payment.Id);
                    cmd.Parameters.AddWithValue("status", newStatus);
                    cmd.Parameters.AddWithValue("paid_at", (object)paidAt! ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("transaction_reference", webhook.TransactionReference);
                    cmd.Parameters.AddWithValue("payment_method", (object)webhook.PaymentMethod! ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 4. Audit
                await InsertAuditAsync(conn, tx, "Payment", payment.Id, newStatus,
                    $"Webhook: {webhook.TransactionReference}, amount={webhook.Amount}", null, ct);

                // 5. If deposit paid → transition shipment PendingDeposit → Matched, quotation → Accepted
                if (newStatus == "Paid" && payment.PaymentType == "Deposit")
                {
                    _logger.LogInformation("Deposit paid for payment {PaymentId}, updating shipment and quotation", payment.Id);

                    // Update quotation status to Accepted
                    const string updateQuotationSql = """
                        UPDATE warehouse.quotations
                        SET status = 'Accepted',
                            accepted_at = NOW(),
                            updated_at = NOW()
                        WHERE id = @quotation_id AND is_deleted = FALSE;
                    """;
                    await using (var cmd2 = new NpgsqlCommand(updateQuotationSql, conn, tx))
                    {
                        cmd2.Parameters.AddWithValue("quotation_id", payment.QuotationId);
                        await cmd2.ExecuteNonQueryAsync(ct);
                    }

                    // Transition shipment: PendingDeposit → Matched
                    await _shipmentStateService.TransitionAsync(
                        payment.ShipmentId,
                        ShipmentStatus.Matched,
                        connection: conn,
                        transaction: tx,
                        performedBy: payment.CustomerId,
                        reason: "Deposit payment received",
                        ct: ct);

                    // Audit shipment transition
                    await InsertAuditAsync(conn, tx, "Shipment", payment.ShipmentId, "Transitioned",
                        "PendingDeposit → Matched (deposit paid)", payment.CustomerId, ct);

                    // Get proposal → customer for notification
                    var proposalId = await GetProposalIdByQuotationAsync(conn, tx, payment.QuotationId, ct);
                    if (proposalId.HasValue)
                    {
                        var proposal = await ReadProposalAsync(conn, tx, proposalId.Value, ct);
                        if (proposal.HasValue)
                        {
                            // Save notification
                            await SaveNotificationAsync(conn, proposal.Value.CustomerId,
                                "Thanh toán cọc thành công",
                                $"Thanh toán cọc cho đơn hàng đã thành công. Hàng hóa sẽ được xử lý tại kho.",
                                "Payment", payment.Id, ct);

                            // SignalR
                            try
                            {
                                await _dispatcher.SendPaymentUpdateToCustomerAsync(proposal.Value.CustomerId, new PaymentEventPayload
                                {
                                    EventType = "DepositPaid",
                                    PaymentId = payment.Id,
                                    ShipmentId = payment.ShipmentId,
                                    Amount = payment.Amount,
                                    Timestamp = DateTime.UtcNow
                                });

                                // Also notify staff
                                await _dispatcher.SendShipmentStatusToStaffAsync(
                                    "Staff",
                                    new ShipmentStatusEventPayload
                                    {
                                        ShipmentId = payment.ShipmentId,
                                        OldStatus = ShipmentStatus.PendingDeposit,
                                        NewStatus = ShipmentStatus.In_Warehouse,
                                        UpdatedAt = DateTime.UtcNow
                                    });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send SignalR for deposit payment");
                            }
                        }
                    }
                }

                // 6. If final payment paid → transition shipment Completed
                if (newStatus == "Paid" && payment.PaymentType == "FinalPayment")
                {
                    _logger.LogInformation("Final payment paid for payment {PaymentId}, completing shipment", payment.Id);

                    // Check all final payments are paid for this quotation
                    var allFinalPaid = await AllFinalPaymentsPaidAsync(conn, tx, payment.QuotationId, ct);
                    if (!allFinalPaid)
                    {
                        _logger.LogInformation("Not all final payments paid yet for quotation {QuotationId}", payment.QuotationId);
                        await tx.CommitAsync(ct);
                        return;
                    }

                    // Transition shipment: Delivered → Completed
                    await _shipmentStateService.TransitionAsync(
                        payment.ShipmentId,
                        ShipmentStatus.Completed,
                        connection: conn,
                        transaction: tx,
                        performedBy: payment.CustomerId,
                        reason: "Final payment received, shipment completed",
                        ct: ct);

                    // Audit shipment transition
                    await InsertAuditAsync(conn, tx, "Shipment", payment.ShipmentId, "Transitioned",
                        "Delivered → Completed (final payment received)", payment.CustomerId, ct);

                    // Get proposal → customer for notification
                    var proposalId = await GetProposalIdByQuotationAsync(conn, tx, payment.QuotationId, ct);
                    if (proposalId.HasValue)
                    {
                        var proposal = await ReadProposalAsync(conn, tx, proposalId.Value, ct);
                        if (proposal.HasValue)
                        {
                            await SaveNotificationAsync(conn, proposal.Value.CustomerId,
                                "Thanh toán hoàn tất",
                                $"Thanh toán cuối cùng đã thành công. Đơn hàng đã hoàn tất.",
                                "Payment", payment.Id, ct);

                            try
                            {
                                await _dispatcher.SendPaymentUpdateToCustomerAsync(proposal.Value.CustomerId, new PaymentEventPayload
                                {
                                    EventType = "FinalPaymentPaid",
                                    PaymentId = payment.Id,
                                    ShipmentId = payment.ShipmentId,
                                    Amount = payment.Amount,
                                    Timestamp = DateTime.UtcNow
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send SignalR for final payment");
                            }
                        }
                    }
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Get payment summary for a shipment.
        /// </summary>
        public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(
            Guid shipmentId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT
                    COALESCE(SUM(CASE WHEN p.payment_type = 'Deposit' AND p.status = 'Paid' THEN p.amount ELSE 0 END), 0) AS deposit_paid,
                    COALESCE(SUM(CASE WHEN p.payment_type = 'FinalPayment' AND p.status = 'Paid' THEN p.amount ELSE 0 END), 0) AS final_paid,
                    COALESCE(SUM(CASE WHEN p.status = 'Paid' THEN p.amount ELSE 0 END), 0) AS total_paid,
                    q.shipping_fee, q.deposit_amount,
                    q.status AS quotation_status,
                    (SELECT p2.transaction_reference FROM warehouse.payments p2
                     WHERE p2.shipment_id = @shipment_id AND p2.status = 'Paid'
                     ORDER BY p2.created_at DESC LIMIT 1) AS transaction_ref
                FROM warehouse.payments p
                JOIN warehouse.quotations q ON q.id = p.quotation_id
                WHERE p.shipment_id = @shipment_id AND p.is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync())
            {
                return new PaymentSummaryDto
                {
                    DepositPaid = 0, FinalPaid = 0, TotalPaid = 0,
                    OutstandingAmount = 0, ShippingFee = 0, DepositAmount = 0
                };
            }

            var depositPaid = reader.GetDecimal(reader.GetOrdinal("deposit_paid"));
            var finalPaid = reader.GetDecimal(reader.GetOrdinal("final_paid"));
            var shippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee"));
            var depositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount"));
            var outstandingAmount = shippingFee - depositPaid - finalPaid;

            return new PaymentSummaryDto
            {
                DepositPaid = depositPaid,
                FinalPaid = finalPaid,
                TotalPaid = depositPaid + finalPaid,
                OutstandingAmount = outstandingAmount < 0 ? 0 : outstandingAmount,
                ShippingFee = shippingFee,
                DepositAmount = depositAmount,
                TransactionRef = reader.IsDBNull(reader.GetOrdinal("transaction_ref")) ? null : reader.GetString(reader.GetOrdinal("transaction_ref")),
                PaymentStatus = reader.GetString(reader.GetOrdinal("quotation_status"))
            };
        }

        /// <summary>
        /// Get payment history for a quotation.
        /// </summary>
        public async Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(
            Guid quotationId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT id, payment_type, amount, currency, status, transaction_reference, paid_at, created_at
                FROM warehouse.payments
                WHERE quotation_id = @quotation_id AND is_deleted = FALSE
                ORDER BY created_at DESC;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("quotation_id", quotationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var list = new List<PaymentHistoryEntry>();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentHistoryEntry
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    PaymentType = reader.GetString(reader.GetOrdinal("payment_type")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                    Currency = reader.GetString(reader.GetOrdinal("currency")),
                    Status = reader.GetString(reader.GetOrdinal("status")),
                    TransactionReference = reader.IsDBNull(reader.GetOrdinal("transaction_reference")) ? null : reader.GetString(reader.GetOrdinal("transaction_reference")),
                    PaidAt = reader.IsDBNull(reader.GetOrdinal("paid_at")) ? null : reader.GetDateTime(reader.GetOrdinal("paid_at")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }
            return list;
        }

        /// <summary>
        /// Calculate outstanding amount for a shipment.
        /// </summary>
        public async Task<decimal> CalculateOutstandingAmountAsync(
            Guid shipmentId, CancellationToken ct)
        {
            var summary = await GetPaymentSummaryAsync(shipmentId, ct);
            return summary.OutstandingAmount;
        }

        // ── Private helpers ──

        private static async Task<(Guid Id, string Status, string PaymentType, decimal Amount,
            string Currency, Guid QuotationId, Guid ShipmentId, Guid CustomerId)?>
            ReadPaymentByTransactionRefAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            string transactionRef, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status, payment_type, amount, currency, quotation_id, shipment_id, customer_id
                FROM warehouse.payments
                WHERE transaction_reference = @ref AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("ref", transactionRef);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                    reader.GetString(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7));
        }

        private static async Task<(Guid Id, string Status, string QuotationCode, decimal ShippingFee,
            decimal DepositAmount, string Currency, Guid ProposalId)?>
            ReadQuotationForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            Guid quotationId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status, quotation_code, shipping_fee, deposit_amount, currency, proposal_id
                FROM warehouse.quotations
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", quotationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                    reader.GetDecimal(4), reader.GetString(5), reader.GetGuid(6));
        }

        private static async Task<(Guid Id, string Status, Guid ShipmentId, Guid CustomerId)?>
            ReadProposalAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            Guid proposalId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status, shipment_id, customer_id
                FROM warehouse.shipment_proposals
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3));
        }

        private static async Task<(Guid Id, string Status, decimal WeightKg, decimal VolumeCbm)?>
            ReadShipmentForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            Guid shipmentId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status, weight_kg, volume_cbm FROM warehouse.shipments
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", shipmentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3));
        }

        private static async Task<Guid?> GetPaidDepositForQuotationAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx, Guid quotationId, CancellationToken ct)
        {
            const string sql = """
                SELECT id FROM warehouse.payments
                WHERE quotation_id = @quotation_id AND payment_type = 'Deposit' AND status = 'Paid' AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("quotation_id", quotationId);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result as Guid?;
        }

        private static async Task<Guid?> GetProposalIdByQuotationAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx, Guid quotationId, CancellationToken ct)
        {
            const string sql = """
                SELECT proposal_id FROM warehouse.quotations
                WHERE id = @quotation_id AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("quotation_id", quotationId);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result as Guid?;
        }

        private static async Task<bool> AllFinalPaymentsPaidAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx, Guid quotationId, CancellationToken ct)
        {
            const string sql = """
                SELECT COUNT(*) FROM warehouse.payments
                WHERE quotation_id = @quotation_id AND payment_type = 'FinalPayment' AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("quotation_id", quotationId);
            var total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

            const string paidSql = """
                SELECT COUNT(*) FROM warehouse.payments
                WHERE quotation_id = @quotation_id AND payment_type = 'FinalPayment' AND status = 'Paid' AND is_deleted = FALSE;
            """;
            await using var paidCmd = new NpgsqlCommand(paidSql, conn, tx);
            paidCmd.Parameters.AddWithValue("quotation_id", quotationId);
            var paid = Convert.ToInt32(await paidCmd.ExecuteScalarAsync(ct));

            return total > 0 && total == paid;
        }

        private static async Task<string> GeneratePaymentCodeAsync(
            NpgsqlConnection conn, string type, CancellationToken ct)
        {
            const string sql = """
                SELECT COUNT(*) FROM warehouse.payments WHERE payment_type = @type;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("type", type);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            var prefix = type == "Deposit" ? "DP" : "FP";
            return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}";
        }

        private static async Task InsertAuditAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx,
            string entityType, Guid entityId, string action, string? details,
            Guid? performedBy, CancellationToken ct)
        {
            const string sql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES (@entity_type, @entity_id, @action, @performed_by, @details::jsonb, NOW());
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("entity_type", entityType);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("performed_by", (object)performedBy! ?? DBNull.Value);
            cmd.Parameters.AddWithValue("details", $"{{\"message\": \"{details}\"}}" ?? "{}");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task SaveNotificationAsync(
            NpgsqlConnection conn, Guid userId, string title, string message,
            string? entityType, Guid? entityId, CancellationToken ct)
        {
            const string sql = """
                INSERT INTO shared.notifications (user_id, title, message, entity_type, entity_id, created_at)
                VALUES (@user_id, @title, @message, @entity_type, @entity_id, NOW());
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("message", message);
            cmd.Parameters.AddWithValue("entity_type", (object)entityType! ?? DBNull.Value);
            cmd.Parameters.AddWithValue("entity_id", (object)entityId! ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
