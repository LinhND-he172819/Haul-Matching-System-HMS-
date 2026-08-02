using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Exceptions;
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
    /// Handles deposit payment, final payment, webhook processing,
    /// retry, cancel, refund, COD, and detail/timeline queries.
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
                    throw new ForbiddenException("Không có quyền thanh toán cho Báo giá này.");

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

                // 8. Notification
                await SendNotificationAsync(customerId,
                    "Deposit Created", $"Thanh toán cọc {quotation.DepositAmount} {quotation.Currency} đã được tạo.",
                    "Payment", paymentId, ct);

                // 9. SignalR
                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(customerId, new PaymentEventPayload
                    {
                        EventType = "PaymentCreated",
                        PaymentId = paymentId,
                        ShipmentId = proposal.ShipmentId,
                        Amount = quotation.DepositAmount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for deposit created"); }

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
                    throw new ForbiddenException("Không có quyền thanh toán cho Báo giá này.");

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

                // 9. Notification
                await SendNotificationAsync(customerId,
                    "Final Payment Created", $"Thanh toán cuối {outstandingAmount} {quotation.Currency} đã được tạo.",
                    "Payment", paymentId, ct);

                // 10. SignalR
                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(customerId, new PaymentEventPayload
                    {
                        EventType = "PaymentCreated",
                        PaymentId = paymentId,
                        ShipmentId = proposal.ShipmentId,
                        Amount = outstandingAmount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for final payment created"); }

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
        /// When customerId is provided (Customer role), verifies ownership via proposal chain.
        /// </summary>
        public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(
            Guid shipmentId, Guid? customerId, CancellationToken ct)
        {
            // Customer ownership verification: shipment must belong to this customer
            if (customerId.HasValue)
            {
                await using var ownershipConn = new NpgsqlConnection(_connStr);
                await ownershipConn.OpenAsync(ct);
                const string ownershipSql = """
                    SELECT COUNT(*) FROM warehouse.shipments s
                    WHERE s.id = @shipment_id AND s.customer_id = @customer_id AND s.is_deleted = FALSE;
                """;
                await using var ownershipCmd = new NpgsqlCommand(ownershipSql, ownershipConn);
                ownershipCmd.Parameters.AddWithValue("shipment_id", shipmentId);
                ownershipCmd.Parameters.AddWithValue("customer_id", customerId.Value);
                var count = Convert.ToInt32(await ownershipCmd.ExecuteScalarAsync(ct));
                if (count == 0)
                    throw new ForbiddenException("Không có quyền xem thông tin thanh toán cho Shipment này.");
            }

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
        /// When customerId is provided (Customer role), verifies ownership via proposal chain.
        /// </summary>
        public async Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(
            Guid quotationId, Guid? customerId, CancellationToken ct)
        {
            // Customer ownership verification: quotation must belong to this customer
            if (customerId.HasValue)
            {
                await using var ownershipConn = new NpgsqlConnection(_connStr);
                await ownershipConn.OpenAsync(ct);
                const string ownershipSql = """
                    SELECT COUNT(*) FROM warehouse.quotations q
                    JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id
                    WHERE q.id = @quotation_id AND sp.customer_id = @customer_id AND q.is_deleted = FALSE;
                """;
                await using var ownershipCmd = new NpgsqlCommand(ownershipSql, ownershipConn);
                ownershipCmd.Parameters.AddWithValue("quotation_id", quotationId);
                ownershipCmd.Parameters.AddWithValue("customer_id", customerId.Value);
                var count = Convert.ToInt32(await ownershipCmd.ExecuteScalarAsync(ct));
                if (count == 0)
                    throw new ForbiddenException("Không có quyền xem lịch sử thanh toán cho Báo giá này.");
            }

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
            Guid shipmentId, Guid? customerId, CancellationToken ct)
        {
            var summary = await GetPaymentSummaryAsync(shipmentId, customerId, ct);
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

        // ═══════════════════════════════════════════════════════
        // Part 1: Payment Retry — Failed → Pending
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Retry a failed payment: Failed → Pending.
        /// Keeps payment_code; updates status, transaction_reference, updated_at.
        /// </summary>
        public async Task<PaymentResponseDto> RetryPaymentAsync(
            Guid paymentId, Guid customerId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // 1. Read payment with lock
                var payment = await ReadPaymentForUpdateAsync(conn, tx, paymentId, ct)
                    ?? throw new InvalidOperationException("Thanh toán không tồn tại.");

                // 2. Ownership check
                if (payment.CustomerId != customerId)
                    throw new ForbiddenException("Không có quyền thao tác trên thanh toán này.");

                // 3. Validate transition: must be Failed
                var currentStatus = Enum.Parse<PaymentStatus>(payment.Status);
                PaymentTransitionGuard.EnsureCanTransition(currentStatus, PaymentStatus.Pending);

                // 4. Update: status → Pending, clear transaction_reference, updated_at = NOW()
                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = 'Pending',
                        transaction_reference = NULL,
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 5. Audit
                await InsertAuditAsync(conn, tx, "Payment", paymentId, "Retry",
                    $"Payment {payment.PaymentCode} retried from Failed to Pending", customerId, ct);

                await tx.CommitAsync(ct);

                // 6. Notification
                await SendNotificationAsync(customerId,
                    "Payment Retry",
                    $"Thanh toán {payment.PaymentCode} đang được thử lại.",
                    "Payment", paymentId, ct);

                // 7. SignalR
                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(customerId, new PaymentEventPayload
                    {
                        EventType = "PaymentRetry",
                        PaymentId = paymentId,
                        ShipmentId = payment.ShipmentId,
                        Amount = payment.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for payment retry"); }

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = payment.PaymentCode,
                    QuotationId = payment.QuotationId,
                    ShipmentId = payment.ShipmentId,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = "Pending",
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Part 3: Payment Cancel — Pending → Cancelled
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Cancel a pending payment: Pending → Cancelled.
        /// No new payment created; shipment/quotation unaffected; can retry later.
        /// </summary>
        public async Task<PaymentResponseDto> CancelPaymentAsync(
            Guid paymentId, Guid customerId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var payment = await ReadPaymentForUpdateAsync(conn, tx, paymentId, ct)
                    ?? throw new InvalidOperationException("Thanh toán không tồn tại.");

                if (payment.CustomerId != customerId)
                    throw new ForbiddenException("Không có quyền thao tác trên thanh toán này.");

                var currentStatus = Enum.Parse<PaymentStatus>(payment.Status);
                PaymentTransitionGuard.EnsureCanTransition(currentStatus, PaymentStatus.Cancelled);

                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = 'Cancelled',
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await InsertAuditAsync(conn, tx, "Payment", paymentId, "Cancelled",
                    $"Payment {payment.PaymentCode} cancelled by customer", customerId, ct);

                await tx.CommitAsync(ct);

                await SendNotificationAsync(customerId,
                    "Payment Cancelled",
                    $"Thanh toán {payment.PaymentCode} đã được hủy.",
                    "Payment", paymentId, ct);

                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(customerId, new PaymentEventPayload
                    {
                        EventType = "PaymentCancelled",
                        PaymentId = paymentId,
                        ShipmentId = payment.ShipmentId,
                        Amount = payment.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for payment cancel"); }

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = payment.PaymentCode,
                    QuotationId = payment.QuotationId,
                    ShipmentId = payment.ShipmentId,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = "Cancelled",
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Part 5: Payment Detail
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Get full payment detail for customer (ownership verified).
        /// </summary>
        public async Task<PaymentDetailDto?> GetPaymentDetailAsync(
            Guid paymentId, Guid? customerId, Guid? staffId, string? role, Guid? hubId,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT
                    p.id, p.payment_code, p.payment_method, p.status, p.payment_type,
                    p.amount, p.currency, p.created_at, p.paid_at, p.transaction_reference,
                    p.failure_reason,
                    q.id AS quotation_id, q.quotation_code, q.shipping_fee, q.deposit_amount,
                    s.id AS shipment_id, s.shipment_code, s.status AS shipment_status,
                    sp.customer_id,
                    COALESCE(cu.full_name, cu.email, 'Customer') AS customer_name
                FROM warehouse.payments p
                JOIN warehouse.quotations q ON q.id = p.quotation_id
                JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id
                JOIN warehouse.shipments s ON s.id = p.shipment_id
                LEFT JOIN identity.users cu ON cu.id = sp.customer_id
                WHERE p.id = @payment_id AND p.is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("payment_id", paymentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;

            var detail = new PaymentDetailDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                PaymentCode = reader.GetString(reader.GetOrdinal("payment_code")),
                PaymentMethod = reader.IsDBNull(reader.GetOrdinal("payment_method")) ? null : reader.GetString(reader.GetOrdinal("payment_method")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                PaymentType = reader.GetString(reader.GetOrdinal("payment_type")),
                Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                PaidAt = reader.IsDBNull(reader.GetOrdinal("paid_at")) ? null : reader.GetDateTime(reader.GetOrdinal("paid_at")),
                TransactionReference = reader.IsDBNull(reader.GetOrdinal("transaction_reference")) ? null : reader.GetString(reader.GetOrdinal("transaction_reference")),
                FailureReason = reader.IsDBNull(reader.GetOrdinal("failure_reason")) ? null : reader.GetString(reader.GetOrdinal("failure_reason")),
                QuotationId = reader.GetGuid(reader.GetOrdinal("quotation_id")),
                QuotationCode = reader.IsDBNull(reader.GetOrdinal("quotation_code")) ? null : reader.GetString(reader.GetOrdinal("quotation_code")),
                ShippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee")),
                DepositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount")),
                ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id")),
                ShipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code")),
                ShipmentStatus = reader.IsDBNull(reader.GetOrdinal("shipment_status")) ? null : reader.GetString(reader.GetOrdinal("shipment_status")),
                CustomerId = reader.GetGuid(reader.GetOrdinal("customer_id")),
                CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
            };

            await reader.CloseAsync();

            // Compute CancelledAt and FailedAt from audit log
            detail.CancelledAt = await GetAuditTimestampAsync(conn, "Payment", paymentId, "Cancelled", ct);
            detail.FailedAt = await GetAuditTimestampAsync(conn, "Payment", paymentId, "Failed", ct);

            // Customer ownership check
            if (customerId.HasValue && detail.CustomerId != customerId.Value)
                throw new ForbiddenException("Không có quyền xem thanh toán này.");

            // Staff hub scoping
            if (staffId.HasValue && role == "Warehouse_Staff" && hubId.HasValue)
            {
                // Check if shipment belongs to staff's hub
                const string hubCheck = """
                    SELECT COUNT(*) FROM warehouse.shipments s
                    WHERE s.id = @shipment_id AND s.hub_id = @hub_id AND s.is_deleted = FALSE;
                """;
                await using var hubCmd = new NpgsqlCommand(hubCheck, conn);
                hubCmd.Parameters.AddWithValue("shipment_id", detail.ShipmentId);
                hubCmd.Parameters.AddWithValue("hub_id", hubId.Value);
                var count = Convert.ToInt32(await hubCmd.ExecuteScalarAsync(ct));
                if (count == 0)
                    throw new ForbiddenException("Shipment không thuộc Hub của bạn.");
            }

            return detail;
        }

        /// <summary>
        /// Staff get payment detail (with hub scoping for Warehouse_Staff, full access for Admin).
        /// </summary>
        public async Task<PaymentDetailDto?> GetStaffPaymentDetailAsync(
            Guid paymentId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct)
        {
            return await GetPaymentDetailAsync(paymentId, null, staffId, role, hubId, ct);
        }

        // ═══════════════════════════════════════════════════════
        // Part 6: Payment Timeline
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Get payment timeline from audit log, ordered by time.
        /// </summary>
        public async Task<List<PaymentTimelineEntry>> GetPaymentTimelineAsync(
            Guid paymentId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT action, details, created_at
                FROM shared.audit_log
                WHERE entity_type = 'Payment' AND entity_id = @payment_id
                ORDER BY created_at ASC;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("payment_id", paymentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var timeline = new List<PaymentTimelineEntry>();
            while (await reader.ReadAsync())
            {
                var action = reader.GetString(reader.GetOrdinal("action"));
                var details = reader.IsDBNull(reader.GetOrdinal("details"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("details"));

                // Parse JSON details message
                var detailsMessage = details;
                if (!string.IsNullOrEmpty(details) && details.Contains("\"message\""))
                {
                    try
                    {
                        var json = System.Text.Json.JsonDocument.Parse(details);
                        if (json.RootElement.TryGetProperty("message", out var msgEl))
                            detailsMessage = msgEl.GetString();
                    }
                    catch { /* keep raw */ }
                }

                timeline.Add(new PaymentTimelineEntry
                {
                    Status = action,
                    Action = action,
                    Details = detailsMessage,
                    OccurredAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }

            return timeline;
        }

        // ═══════════════════════════════════════════════════════
        // Part 7: Refund — Paid → PendingRefund → Refunded
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Request refund: Paid → PendingRefund. Staff-initiated.
        /// </summary>
        public async Task<PaymentResponseDto> RequestRefundAsync(
            Guid paymentId, Guid staffId, string reason, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var payment = await ReadPaymentForUpdateAsync(conn, tx, paymentId, ct)
                    ?? throw new InvalidOperationException("Thanh toán không tồn tại.");

                var currentStatus = Enum.Parse<PaymentStatus>(payment.Status);
                PaymentTransitionGuard.EnsureCanTransition(currentStatus, PaymentStatus.PendingRefund);

                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = 'PendingRefund',
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await InsertAuditAsync(conn, tx, "Payment", paymentId, "RefundRequested",
                    $"Refund requested: {reason}", staffId, ct);

                await tx.CommitAsync(ct);

                // Notification to customer
                var customerId = payment.CustomerId;
                await SendNotificationAsync(customerId,
                    "Refund Requested",
                    $"Yêu cầu hoàn tiền cho thanh toán {payment.PaymentCode} đã được gửi.",
                    "Payment", paymentId, ct);

                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(customerId, new PaymentEventPayload
                    {
                        EventType = "RefundRequested",
                        PaymentId = paymentId,
                        ShipmentId = payment.ShipmentId,
                        Amount = payment.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for refund requested"); }

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = payment.PaymentCode,
                    QuotationId = payment.QuotationId,
                    ShipmentId = payment.ShipmentId,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = "PendingRefund",
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Approve refund: PendingRefund → Refunded. Admin-initiated.
        /// If deposit refund → Shipment: Matched → PendingReview.
        /// </summary>
        public async Task<PaymentResponseDto> ApproveRefundAsync(
            Guid paymentId, Guid staffId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var payment = await ReadPaymentForUpdateAsync(conn, tx, paymentId, ct)
                    ?? throw new InvalidOperationException("Thanh toán không tồn tại.");

                var currentStatus = Enum.Parse<PaymentStatus>(payment.Status);
                PaymentTransitionGuard.EnsureCanTransition(currentStatus, PaymentStatus.Refunded);

                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = 'Refunded',
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await InsertAuditAsync(conn, tx, "Payment", paymentId, "RefundApproved",
                    $"Refund approved for {payment.PaymentCode}: {payment.Amount} {payment.Currency}", staffId, ct);

                // If Deposit refund → revert shipment Matched → PendingReview
                if (payment.PaymentType == "Deposit")
                {
                    // Read current shipment status
                    var shipment = await ReadShipmentForUpdateAsync(conn, tx, payment.ShipmentId, ct);
                    if (shipment.HasValue && shipment.Value.Status == ShipmentStatus.Matched.ToString())
                    {
                        await _shipmentStateService.TransitionAsync(
                            payment.ShipmentId,
                            ShipmentStatus.PendingReview,
                            connection: conn,
                            transaction: tx,
                            performedBy: staffId,
                            reason: "Deposit refunded, shipment reverted to PendingReview",
                            ct: ct);

                        await InsertAuditAsync(conn, tx, "Shipment", payment.ShipmentId, "Transitioned",
                            "Matched → PendingReview (deposit refund)", staffId, ct);

                        // Also cancel quotation
                        var quotation = await ReadQuotationForUpdateAsync(conn, tx, payment.QuotationId, ct);
                        if (quotation.HasValue && quotation.Value.Status == "Accepted")
                        {
                            const string cancelQSql = """
                                UPDATE warehouse.quotations
                                SET status = 'Cancelled', cancelled_at = NOW(), updated_at = NOW()
                                WHERE id = @quotation_id AND is_deleted = FALSE;
                            """;
                            await using var qCmd = new NpgsqlCommand(cancelQSql, conn, tx);
                            qCmd.Parameters.AddWithValue("quotation_id", payment.QuotationId);
                            await qCmd.ExecuteNonQueryAsync(ct);
                        }
                    }
                }

                await tx.CommitAsync(ct);

                // Notification
                await SendNotificationAsync(payment.CustomerId,
                    "Refund Completed",
                    $"Hoàn tiền cho thanh toán {payment.PaymentCode} đã hoàn tất.",
                    "Payment", paymentId, ct);

                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(payment.CustomerId, new PaymentEventPayload
                    {
                        EventType = "RefundCompleted",
                        PaymentId = paymentId,
                        ShipmentId = payment.ShipmentId,
                        Amount = payment.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for refund completed"); }

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = payment.PaymentCode,
                    QuotationId = payment.QuotationId,
                    ShipmentId = payment.ShipmentId,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = "Refunded",
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Part 8: COD — Driver confirms, no webhook
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Confirm COD payment: Pending → Paid. Driver-initiated after delivery.
        /// Then transitions shipment Delivered → Completed.
        /// </summary>
        public async Task<PaymentResponseDto> ConfirmCodPaymentAsync(
            Guid paymentId, Guid driverId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var payment = await ReadPaymentForUpdateAsync(conn, tx, paymentId, ct)
                    ?? throw new InvalidOperationException("Thanh toán không tồn tại.");

                if (payment.PaymentType != "FinalPayment")
                    throw new InvalidOperationException("COD chỉ áp dụng cho thanh toán cuối.");

                if (payment.PaymentMethod != "COD")
                    throw new InvalidOperationException("Thanh toán này không phải COD.");

                // Verify driver owns a trip carrying this shipment
                const string driverCheckSql = """
                    SELECT COUNT(1) FROM transport.trip_shipments ts
                    JOIN transport.trips t ON t.id = ts.trip_id AND t.driver_id = @driver_id AND t.is_deleted = FALSE
                    WHERE ts.shipment_id = @shipment_id AND ts.is_deleted = FALSE;
                """;
                await using (var checkCmd = new NpgsqlCommand(driverCheckSql, conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("driver_id", driverId);
                    checkCmd.Parameters.AddWithValue("shipment_id", payment.ShipmentId);
                    var hasTrip = (long)(await checkCmd.ExecuteScalarAsync(ct))! > 0;
                    if (!hasTrip)
                        throw new ForbiddenException("Bạn không có quyền xác nhận thanh toán COD này.");
                }

                var currentStatus = Enum.Parse<PaymentStatus>(payment.Status);
                PaymentTransitionGuard.EnsureCanTransition(currentStatus, PaymentStatus.Paid);

                // Update payment
                const string updateSql = """
                    UPDATE warehouse.payments
                    SET status = 'Paid',
                        paid_at = NOW(),
                        confirmed_at = NOW(),
                        confirmed_by = @driver_id,
                        transaction_reference = @driver_ref,
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", paymentId);
                    cmd.Parameters.AddWithValue("driver_id", driverId);
                    cmd.Parameters.AddWithValue("driver_ref", $"COD-{driverId:N}");
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await InsertAuditAsync(conn, tx, "Payment", paymentId, "Paid",
                    $"COD confirmed by driver {driverId}", driverId, ct);

                // Check all final payments for this quotation
                var allFinalPaid = await AllFinalPaymentsPaidAsync(conn, tx, payment.QuotationId, ct);
                if (allFinalPaid)
                {
                    // Transition shipment: Delivered → Completed
                    await _shipmentStateService.TransitionAsync(
                        payment.ShipmentId,
                        ShipmentStatus.Completed,
                        connection: conn,
                        transaction: tx,
                        performedBy: driverId,
                        reason: "COD payment confirmed, shipment completed",
                        ct: ct);

                    await InsertAuditAsync(conn, tx, "Shipment", payment.ShipmentId, "Transitioned",
                        "Delivered → Completed (COD confirmed)", driverId, ct);
                }

                await tx.CommitAsync(ct);

                // Notification
                await SendNotificationAsync(payment.CustomerId,
                    "Final Payment Paid",
                    $"Thanh toán COD {payment.PaymentCode} đã xác nhận.",
                    "Payment", paymentId, ct);

                try
                {
                    await _dispatcher.SendPaymentUpdateToCustomerAsync(payment.CustomerId, new PaymentEventPayload
                    {
                        EventType = "FinalPaymentPaid",
                        PaymentId = paymentId,
                        ShipmentId = payment.ShipmentId,
                        Amount = payment.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR failed for COD confirmation"); }

                return new PaymentResponseDto
                {
                    Id = paymentId,
                    PaymentCode = payment.PaymentCode,
                    QuotationId = payment.QuotationId,
                    ShipmentId = payment.ShipmentId,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = "Paid",
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Private helpers
        // ═══════════════════════════════════════════════════════

        private async Task<(Guid Id, string Status, string PaymentType, decimal Amount,
            string Currency, Guid QuotationId, Guid ShipmentId, Guid CustomerId,
            string PaymentCode, string? PaymentMethod, DateTime CreatedAt)?
            > ReadPaymentForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            Guid paymentId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status, payment_type, amount, currency, quotation_id, shipment_id, customer_id,
                       payment_code, payment_method, created_at
                FROM warehouse.payments
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", paymentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                    reader.GetString(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7),
                    reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetDateTime(10));
        }

        private static async Task<DateTime?> GetAuditTimestampAsync(
            NpgsqlConnection conn, string entityType, Guid entityId, string action, CancellationToken ct)
        {
            const string sql = """
                SELECT created_at FROM shared.audit_log
                WHERE entity_type = @entity_type AND entity_id = @entity_id AND action = @action
                ORDER BY created_at DESC LIMIT 1;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("entity_type", entityType);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            cmd.Parameters.AddWithValue("action", action);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result as DateTime?;
        }

        private async Task SendNotificationAsync(
            Guid userId, string title, string message,
            string? entityType, Guid? entityId, CancellationToken ct)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connStr);
                await conn.OpenAsync(ct);
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification to {UserId}", userId);
            }
        }
    }
}
