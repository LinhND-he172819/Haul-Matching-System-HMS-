using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
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
    /// Quotation service using raw Npgsql with transaction support.
    /// Manages Draft → Sent → Accepted/Accepted lifecycle.
    /// </summary>
    public class QuotationService : IQuotationService
    {
        private readonly string _connStr;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<QuotationService> _logger;

        public QuotationService(
            IConfiguration configuration,
            IShipmentStateService shipmentStateService,
            IRealtimeDispatcher dispatcher,
            ILogger<QuotationService> logger)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
            _shipmentStateService = shipmentStateService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        /// <summary>
        /// Create a new quotation (Draft) for an Approved proposal.
        /// </summary>
        public async Task<QuotationResponseDto> CreateQuotationAsync(
            Guid proposalId, CreateQuotationRequest request,
            Guid staffId, CancellationToken ct)
        {
            if (request.ShippingFee <= 0)
                throw new InvalidOperationException("Phí vận chuyển phải lớn hơn 0.");
            if (request.DepositAmount <= 0)
                throw new InvalidOperationException("Tiền cọc phải lớn hơn 0.");
            if (request.DepositAmount >= request.ShippingFee)
                throw new InvalidOperationException("Tiền cọc phải nhỏ hơn tổng phí vận chuyển.");
            if (request.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Thời hạn báo giá phải ở tương lai.");

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // Verify proposal is Approved and no active quotation exists
                var proposal = await ReadProposalAsync(conn, tx, proposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                if (proposal.Status != ProposalStatusConstants.Approved)
                    throw new InvalidOperationException(
                        $"Chỉ có thể tạo Báo giá khi Proposal ở trạng thái Approved. Hiện tại: {proposal.Status}");

                // Check no Draft/Sent quotation exists
                var hasActive = await HasActiveQuotationAsync(conn, tx, proposalId, ct);
                if (hasActive)
                    throw new InvalidOperationException("Proposal đã có một Báo giá chưa xử lý.");

                // Generate quotation code
                var quotationCode = await GenerateQuotationCodeAsync(conn, ct);

                // Insert quotation
                const string sql = """
                    INSERT INTO warehouse.quotations
                        (id, proposal_id, quotation_code, shipping_fee, deposit_amount, currency,
                         status, quoted_by, quoted_at, expires_at, created_at, updated_at, is_deleted)
                    VALUES
                        (@id, @proposal_id, @quotation_code, @shipping_fee, @deposit_amount, @currency,
                         'Draft', @quoted_by, NOW(), @expires_at, NOW(), NOW(), FALSE)
                    RETURNING id, created_at;
                """;

                var id = Guid.NewGuid();
                await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.Parameters.AddWithValue("proposal_id", proposalId);
                    cmd.Parameters.AddWithValue("quotation_code", quotationCode);
                    cmd.Parameters.AddWithValue("shipping_fee", request.ShippingFee);
                    cmd.Parameters.AddWithValue("deposit_amount", request.DepositAmount);
                    cmd.Parameters.AddWithValue("currency", request.Currency);
                    cmd.Parameters.AddWithValue("quoted_by", staffId);
                    cmd.Parameters.AddWithValue("expires_at", request.ExpiresAt);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Audit
                await InsertAuditAsync(conn, tx, "Quotation", id, "Created",
                    $"Created quotation {quotationCode} for proposal, fee={request.ShippingFee}, deposit={request.DepositAmount}",
                    staffId, ct);

                await tx.CommitAsync(ct);

                return new QuotationResponseDto
                {
                    Id = id,
                    ProposalId = proposalId,
                    QuotationCode = quotationCode,
                    ShippingFee = request.ShippingFee,
                    DepositAmount = request.DepositAmount,
                    Currency = request.Currency,
                    Status = "Draft",
                    QuotedBy = staffId,
                    QuotedAt = DateTime.UtcNow,
                    ExpiresAt = request.ExpiresAt,
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
        /// Update a quotation in Draft status.
        /// </summary>
        public async Task<QuotationResponseDto> UpdateQuotationAsync(
            Guid quotationId, UpdateQuotationRequest request,
            Guid staffId, CancellationToken ct)
        {
            if (request.ShippingFee <= 0)
                throw new InvalidOperationException("Phí vận chuyển phải lớn hơn 0.");
            if (request.DepositAmount <= 0)
                throw new InvalidOperationException("Tiền cọc phải lớn hơn 0.");
            if (request.DepositAmount >= request.ShippingFee)
                throw new InvalidOperationException("Tiền cọc phải nhỏ hơn tổng phí vận chuyển.");

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                var quotation = await ReadQuotationForUpdateAsync(conn, tx, quotationId, ct)
                    ?? throw new InvalidOperationException("Báo giá không tồn tại.");

                QuotationTransitionGuard.EnsureCanTransition(
                    Enum.Parse<QuotationStatus>(quotation.Status), QuotationStatus.Draft);

                if (quotation.Status != "Draft")
                    throw new InvalidOperationException("Chỉ có thể chỉnh sửa Báo giá ở trạng thái Draft.");

                const string sql = """
                    UPDATE warehouse.quotations
                    SET shipping_fee = @shipping_fee,
                        deposit_amount = @deposit_amount,
                        currency = @currency,
                        expires_at = @expires_at,
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", quotationId);
                    cmd.Parameters.AddWithValue("shipping_fee", request.ShippingFee);
                    cmd.Parameters.AddWithValue("deposit_amount", request.DepositAmount);
                    cmd.Parameters.AddWithValue("currency", request.Currency);
                    cmd.Parameters.AddWithValue("expires_at", request.ExpiresAt);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await InsertAuditAsync(conn, tx, "Quotation", quotationId, "Updated",
                    $"Updated fee={request.ShippingFee}, deposit={request.DepositAmount}", staffId, ct);

                await tx.CommitAsync(ct);

                return new QuotationResponseDto
                {
                    Id = quotationId,
                    ProposalId = quotation.ProposalId,
                    QuotationCode = quotation.QuotationCode,
                    ShippingFee = request.ShippingFee,
                    DepositAmount = request.DepositAmount,
                    Currency = request.Currency,
                    Status = quotation.Status,
                    QuotedBy = staffId,
                    QuotedAt = quotation.QuotedAt,
                    ExpiresAt = request.ExpiresAt,
                    CreatedAt = quotation.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Send a quotation: Draft → Sent.
        /// Also transitions Shipment: Approved → PendingDeposit.
        /// </summary>
        public async Task SendQuotationAsync(
            Guid quotationId, Guid staffId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var quotation = await ReadQuotationForUpdateAsync(conn, tx, quotationId, ct)
                    ?? throw new InvalidOperationException("Báo giá không tồn tại.");

                QuotationTransitionGuard.EnsureCanTransition(
                    Enum.Parse<QuotationStatus>(quotation.Status), QuotationStatus.Sent);

                if (quotation.Status != "Draft")
                    throw new InvalidOperationException("Chỉ có thể gửi Báo giá từ trạng thái Draft.");

                // Update quotation: Draft → Sent
                const string updateSql = """
                    UPDATE warehouse.quotations
                    SET status = 'Sent',
                        sent_at = NOW(),
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", quotationId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Transition shipment to PendingDeposit
                var proposal = await ReadProposalAsync(conn, tx, quotation.ProposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                await _shipmentStateService.TransitionAsync(
                    proposal.ShipmentId,
                    ShipmentStatus.PendingDeposit,
                    connection: conn,
                    transaction: tx,
                    performedBy: staffId,
                    reason: $"Quotation {quotation.QuotationCode} sent to customer",
                    ct: ct);

                // Audit
                await InsertAuditAsync(conn, tx, "Quotation", quotationId, "Sent",
                    $"Quotation {quotation.QuotationCode} sent to customer", staffId, ct);

                await tx.CommitAsync(ct);

                // Notification
                await SaveNotificationAsync(conn, proposal.CustomerId,
                    "Báo giá mới",
                    $"Bạn có báo giá mới cho đề xuất. Vui lòng xem và thanh toán tiền cọc.",
                    "Quotation", quotationId, ct);

                // SignalR
                try
                {
                    await _dispatcher.SendQuotationToCustomerAsync(proposal.CustomerId, new QuotationEventPayload
                    {
                        EventType = "QuotationSent",
                        QuotationId = quotationId,
                        ProposalId = quotation.ProposalId,
                        ShippingFee = quotation.ShippingFee,
                        DepositAmount = quotation.DepositAmount,
                        ExpiresAt = quotation.ExpiresAt ?? DateTime.UtcNow.AddHours(24),
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR for quotation sent");
                }

                _logger.LogInformation("Quotation {QuotationId} sent to customer for proposal {ProposalId}",
                    quotationId, quotation.ProposalId);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Cancel a quotation: Draft/Sent → Cancelled.
        /// </summary>
        public async Task CancelQuotationAsync(
            Guid quotationId, Guid staffId, string? reason, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                var quotation = await ReadQuotationForUpdateAsync(conn, tx, quotationId, ct)
                    ?? throw new InvalidOperationException("Báo giá không tồn tại.");

                var currentStatus = Enum.Parse<QuotationStatus>(quotation.Status);
                QuotationTransitionGuard.EnsureCanTransition(currentStatus, QuotationStatus.Cancelled);

                // Update status
                const string updateSql = """
                    UPDATE warehouse.quotations
                    SET status = 'Cancelled',
                        cancelled_at = NOW(),
                        updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", quotationId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // If was Sent, revert shipment status back to PendingReview
                if (quotation.Status == "Sent")
                {
                    var proposal = await ReadProposalAsync(conn, tx, quotation.ProposalId, ct);
                    if (proposal.HasValue)
                    {
                        await _shipmentStateService.TransitionAsync(
                            proposal.Value.ShipmentId,
                            ShipmentStatus.PendingReview,
                            connection: conn,
                            transaction: tx,
                            performedBy: staffId,
                            reason: $"Quotation cancelled: {reason}",
                            ct: ct);
                    }
                }

                await InsertAuditAsync(conn, tx, "Quotation", quotationId, "Cancelled",
                    $"Reason: {reason}", staffId, ct);

                await tx.CommitAsync(ct);

                _logger.LogInformation("Quotation {QuotationId} cancelled: {Reason}", quotationId, reason);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Get quotation details by ID.
        /// </summary>
        public async Task<QuotationResponseDto?> GetQuotationAsync(Guid quotationId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT id, proposal_id, quotation_code, shipping_fee, deposit_amount, currency,
                       status, quoted_by, quoted_at, sent_at, expires_at, accepted_at, created_at
                FROM warehouse.quotations
                WHERE id = @id AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", quotationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;

            return MapToResponse(reader);
        }

        /// <summary>
        /// Get active (Draft or Sent) quotation for a proposal.
        /// </summary>
        public async Task<QuotationSummaryDto?> GetActiveQuotationForProposalAsync(
            Guid proposalId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT id, quotation_code, shipping_fee, deposit_amount, currency, status,
                       sent_at, expires_at, accepted_at, created_at
                FROM warehouse.quotations
                WHERE proposal_id = @proposal_id AND is_deleted = FALSE
                  AND status IN ('Draft', 'Sent')
                ORDER BY created_at DESC LIMIT 1;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;

            return MapToSummary(reader);
        }

        /// <summary>
        /// Get quotation for customer view (with ownership check).
        /// </summary>
        public async Task<QuotationSummaryDto?> GetQuotationForCustomerAsync(
            Guid quotationId, Guid customerId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT q.id, q.quotation_code, q.shipping_fee, q.deposit_amount, q.currency, q.status,
                       q.sent_at, q.expires_at, q.accepted_at, q.created_at
                FROM warehouse.quotations q
                JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id
                WHERE q.id = @id AND q.is_deleted = FALSE
                  AND sp.customer_id = @customer_id;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", quotationId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;

            return MapToSummary(reader);
        }

        /// <summary>
        /// Background worker: expire quotations past their ExpiresAt.
        /// Returns count of expired quotations.
        /// </summary>
        public async Task<int> ExpireOverdueQuotationsAsync(CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                UPDATE warehouse.quotations
                SET status = 'Expired',
                    expired_at = NOW(),
                    updated_at = NOW()
                WHERE status = 'Sent'
                  AND expires_at < NOW()
                  AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            var count = await cmd.ExecuteNonQueryAsync(ct);

            if (count > 0)
                _logger.LogInformation("Expired {Count} overdue quotations", count);

            return count;
        }

        // ── Private helpers ──

        private async Task<(Guid Id, string Status, string QuotationCode, decimal ShippingFee,
            decimal DepositAmount, string Currency, Guid? QuotedBy, DateTime? QuotedAt,
            DateTime? ExpiresAt, DateTime CreatedAt, Guid ProposalId)?>
            ReadQuotationForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            Guid quotationId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, proposal_id, status, quotation_code, shipping_fee, deposit_amount,
                       currency, quoted_by, quoted_at, expires_at, created_at
                FROM warehouse.quotations
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", quotationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;

            return (reader.GetGuid(0), reader.GetString(2), reader.GetString(3),
                    reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetGuid(7),
                    reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    reader.GetDateTime(10), reader.GetGuid(1));
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

        private static async Task<bool> HasActiveQuotationAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx, Guid proposalId, CancellationToken ct)
        {
            const string sql = """
                SELECT COUNT(*) FROM warehouse.quotations
                WHERE proposal_id = @proposal_id AND is_deleted = FALSE
                  AND status IN ('Draft', 'Sent');
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result) > 0;
        }

        private static async Task<string> GenerateQuotationCodeAsync(NpgsqlConnection conn, CancellationToken ct)
        {
            const string sql = """
                SELECT COUNT(*) FROM warehouse.quotations;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return $"QT-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}";
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

        private static QuotationResponseDto MapToResponse(NpgsqlDataReader reader)
        {
            return new QuotationResponseDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                ProposalId = reader.GetGuid(reader.GetOrdinal("proposal_id")),
                QuotationCode = reader.GetString(reader.GetOrdinal("quotation_code")),
                ShippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee")),
                DepositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                QuotedBy = reader.IsDBNull(reader.GetOrdinal("quoted_by")) ? null : reader.GetGuid(reader.GetOrdinal("quoted_by")),
                QuotedAt = reader.IsDBNull(reader.GetOrdinal("quoted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("quoted_at")),
                SentAt = reader.IsDBNull(reader.GetOrdinal("sent_at")) ? null : reader.GetDateTime(reader.GetOrdinal("sent_at")),
                ExpiresAt = reader.IsDBNull(reader.GetOrdinal("expires_at")) ? null : reader.GetDateTime(reader.GetOrdinal("expires_at")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            };
        }

        private static QuotationSummaryDto MapToSummary(NpgsqlDataReader reader)
        {
            var shippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee"));
            var depositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount"));
            return new QuotationSummaryDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                QuotationCode = reader.GetString(reader.GetOrdinal("quotation_code")),
                ShippingFee = shippingFee,
                DepositAmount = depositAmount,
                RemainingAmount = shippingFee - depositAmount,
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                SentAt = reader.IsDBNull(reader.GetOrdinal("sent_at")) ? null : reader.GetDateTime(reader.GetOrdinal("sent_at")),
                ExpiresAt = reader.IsDBNull(reader.GetOrdinal("expires_at")) ? null : reader.GetDateTime(reader.GetOrdinal("expires_at")),
                AcceptedAt = reader.IsDBNull(reader.GetOrdinal("accepted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("accepted_at")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            };
        }
    }
}
