using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Matching.Application.Services
{
    /// <summary>
    /// Service for Warehouse Staff / Admin proposal management.
    /// Uses direct Npgsql for transaction support (same pattern as ShipmentStateService).
    /// </summary>
    public class StaffProposalService : IStaffProposalService
    {
        private readonly string _connStr;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<StaffProposalService> _logger;

        public StaffProposalService(
            IConfiguration configuration,
            IShipmentStateService shipmentStateService,
            IRealtimeDispatcher dispatcher,
            ILogger<StaffProposalService> logger)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
            _shipmentStateService = shipmentStateService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task<PagedResult<StaffProposalSummaryDto>> GetProposalsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, int page, int pageSize,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var whereClauses = new List<string> { "sp.is_deleted = FALSE" };
            var parameters = new List<NpgsqlParameter>();

            // Hub filtering: Staff only sees proposals for trips originating from their hub
            if (role == "Warehouse_Staff" && hubId.HasValue)
            {
                whereClauses.Add("tp.created_by_staff_hub = @hubId");
                parameters.Add(new NpgsqlParameter("hubId", hubId.Value));
            }

            if (!string.IsNullOrEmpty(status))
            {
                whereClauses.Add("sp.status = @status");
                parameters.Add(new NpgsqlParameter("status", status));
            }

            var whereSql = string.Join(" AND ", whereClauses);

            // Count
            var countSql = $"""
                SELECT COUNT(*)
                FROM warehouse.shipment_proposals sp
                LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                WHERE {whereSql};
            """;
            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                var result = await countCmd.ExecuteScalarAsync(ct);
                var totalCount = Convert.ToInt32(result ?? 0);

                // Paged query
                var offset = (page - 1) * pageSize;
                var querySql = $"""
                    SELECT
                        sp.id AS proposal_id,
                        sp.status,
                        sp.created_at,
                        sp.sender_name,
                        sp.sender_phone,
                        sp.pickup_address,
                        s.id AS shipment_id,
                        s.commodity,
                        s.weight_kg,
                        s.volume_cbm,
                        s.receiver_name,
                        s.receiver_phone,
                        s.dest_address AS delivery_address,
                        tp.id AS trip_post_id,
                        tp.title AS trip_title,
                        tp.origin,
                        tp.destination,
                        tp.departure_time,
                        tp.max_weight AS max_weight,
                        tp.max_volume AS max_volume,
                        t.id AS trip_id,
                        t.trip_code,
                        t.driver_id,
                        t.current_load_weight,
                        t.current_load_volume,
                        v.max_weight_kg AS vehicle_max_weight,
                        v.max_volume_cbm AS vehicle_max_volume,
                        c.id AS customer_id,
                        c.full_name AS customer_name,
                        c.phone AS customer_phone
                    FROM warehouse.shipment_proposals sp
                    JOIN warehouse.shipments s ON s.id = sp.shipment_id
                    LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                    LEFT JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                    LEFT JOIN transport.vehicles v ON v.id = t.vehicle_id
                    LEFT JOIN identity.users c ON c.id = sp.customer_id AND c.is_deleted = FALSE
                    WHERE {whereSql}
                    ORDER BY sp.created_at DESC
                    LIMIT @limit OFFSET @offset;
                """;

                var allParams = parameters.ToList();
                allParams.Add(new NpgsqlParameter("limit", pageSize));
                allParams.Add(new NpgsqlParameter("offset", offset));

                await using var cmd = new NpgsqlCommand(querySql, conn);
                cmd.Parameters.AddRange(allParams.ToArray());

                var items = new List<StaffProposalSummaryDto>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync())
                {
                    var maxWeight = reader.IsDBNull(reader.GetOrdinal("vehicle_max_weight")) ? 0m : reader.GetDecimal(reader.GetOrdinal("vehicle_max_weight"));
                    var maxVolume = reader.IsDBNull(reader.GetOrdinal("vehicle_max_volume")) ? 0m : reader.GetDecimal(reader.GetOrdinal("vehicle_max_volume"));
                    var currentWeight = reader.IsDBNull(reader.GetOrdinal("current_load_weight")) ? 0m : reader.GetDecimal(reader.GetOrdinal("current_load_weight"));
                    var currentVolume = reader.IsDBNull(reader.GetOrdinal("current_load_volume")) ? 0m : reader.GetDecimal(reader.GetOrdinal("current_load_volume"));

                    items.Add(new StaffProposalSummaryDto
                    {
                        ProposalId = reader.GetGuid(reader.GetOrdinal("proposal_id")),
                        Status = reader.GetString(reader.GetOrdinal("status")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        SenderName = reader.GetString(reader.GetOrdinal("sender_name")),
                        SenderPhone = reader.GetString(reader.GetOrdinal("sender_phone")),
                        PickupAddress = reader.GetString(reader.GetOrdinal("pickup_address")),
                        ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id")),
                        Commodity = reader.IsDBNull(reader.GetOrdinal("commodity")) ? null : reader.GetString(reader.GetOrdinal("commodity")),
                        WeightKg = reader.GetDecimal(reader.GetOrdinal("weight_kg")),
                        VolumeCbm = reader.GetDecimal(reader.GetOrdinal("volume_cbm")),
                        ReceiverName = reader.IsDBNull(reader.GetOrdinal("receiver_name")) ? null : reader.GetString(reader.GetOrdinal("receiver_name")),
                        ReceiverPhone = reader.IsDBNull(reader.GetOrdinal("receiver_phone")) ? null : reader.GetString(reader.GetOrdinal("receiver_phone")),
                        DeliveryAddress = reader.IsDBNull(reader.GetOrdinal("delivery_address")) ? null : reader.GetString(reader.GetOrdinal("delivery_address")),
                        TripPostId = reader.IsDBNull(reader.GetOrdinal("trip_post_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("trip_post_id")),
                        TripId = reader.IsDBNull(reader.GetOrdinal("trip_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("trip_id")),
                        TripCode = reader.IsDBNull(reader.GetOrdinal("trip_code")) ? null : reader.GetString(reader.GetOrdinal("trip_code")),
                        Origin = reader.IsDBNull(reader.GetOrdinal("origin")) ? null : reader.GetString(reader.GetOrdinal("origin")),
                        Destination = reader.IsDBNull(reader.GetOrdinal("destination")) ? null : reader.GetString(reader.GetOrdinal("destination")),
                        DepartureTime = reader.IsDBNull(reader.GetOrdinal("departure_time")) ? null : reader.GetDateTime(reader.GetOrdinal("departure_time")),
                        RemainingWeight = maxWeight - currentWeight,
                        RemainingVolume = maxVolume - currentVolume,
                        CustomerId = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("customer_id")),
                        CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? "N/A" : reader.GetString(reader.GetOrdinal("customer_name")),
                        CustomerPhone = reader.IsDBNull(reader.GetOrdinal("customer_phone")) ? null : reader.GetString(reader.GetOrdinal("customer_phone"))
                    });
                }

                return new PagedResult<StaffProposalSummaryDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
        }

        public async Task<StaffProposalDetailDto?> GetProposalDetailAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT
                    sp.id AS proposal_id, sp.status, sp.created_at, sp.reviewer_id, sp.reviewer_name,
                    sp.approved_at, sp.rejected_at, sp.reject_reason,
                    sp.sender_name, sp.sender_phone, sp.pickup_address, sp.pickup_latitude, sp.pickup_longitude, sp.pickup_note,
                    s.id AS shipment_id, s.commodity, s.weight_kg, s.volume_cbm, s.status AS shipment_status,
                    s.receiver_name, s.receiver_phone, s.dest_address AS delivery_address, s.special_handling_note,
                    s.shipment_code,
                    tp.id AS trip_post_id, tp.title, tp.origin, tp.destination, tp.departure_time,
                    tp.accept_until, tp.pickup_mode, tp.max_weight, tp.max_volume,
                    t.id AS trip_id, t.trip_code, t.driver_id, t.current_load_weight, t.current_load_volume,
                    v.max_weight_kg AS vehicle_max_weight, v.max_volume_cbm AS vehicle_max_volume,
                    c.id AS customer_id, c.full_name AS customer_name, c.phone AS customer_phone, c.email AS customer_email
                FROM warehouse.shipment_proposals sp
                JOIN warehouse.shipments s ON s.id = sp.shipment_id
                LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                LEFT JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                LEFT JOIN transport.vehicles v ON v.id = t.vehicle_id
                LEFT JOIN identity.users c ON c.id = sp.customer_id AND c.is_deleted = FALSE
                WHERE sp.id = @proposal_id AND sp.is_deleted = FALSE;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync()) return null;

            var maxWeight = reader.IsDBNull(reader.GetOrdinal("vehicle_max_weight")) ? 0m : reader.GetDecimal(reader.GetOrdinal("vehicle_max_weight"));
            var maxVolume = reader.IsDBNull(reader.GetOrdinal("vehicle_max_volume")) ? 0m : reader.GetDecimal(reader.GetOrdinal("vehicle_max_volume"));
            var currentWeight = reader.IsDBNull(reader.GetOrdinal("current_load_weight")) ? 0m : reader.GetDecimal(reader.GetOrdinal("current_load_weight"));
            var currentVolume = reader.IsDBNull(reader.GetOrdinal("current_load_volume")) ? 0m : reader.GetDecimal(reader.GetOrdinal("current_load_volume"));

            var detail = new StaffProposalDetailDto
            {
                ProposalId = reader.GetGuid(reader.GetOrdinal("proposal_id")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                ApprovedAt = reader.IsDBNull(reader.GetOrdinal("approved_at")) ? null : reader.GetDateTime(reader.GetOrdinal("approved_at")),
                RejectedAt = reader.IsDBNull(reader.GetOrdinal("rejected_at")) ? null : reader.GetDateTime(reader.GetOrdinal("rejected_at")),
                RejectReason = reader.IsDBNull(reader.GetOrdinal("reject_reason")) ? null : reader.GetString(reader.GetOrdinal("reject_reason")),
                SenderName = reader.GetString(reader.GetOrdinal("sender_name")),
                SenderPhone = reader.GetString(reader.GetOrdinal("sender_phone")),
                PickupAddress = reader.GetString(reader.GetOrdinal("pickup_address")),
                PickupLatitude = reader.IsDBNull(reader.GetOrdinal("pickup_latitude")) ? null : reader.GetDouble(reader.GetOrdinal("pickup_latitude")),
                PickupLongitude = reader.IsDBNull(reader.GetOrdinal("pickup_longitude")) ? null : reader.GetDouble(reader.GetOrdinal("pickup_longitude")),
                PickupNote = reader.IsDBNull(reader.GetOrdinal("pickup_note")) ? null : reader.GetString(reader.GetOrdinal("pickup_note")),
                Shipment = new ShipmentInfoDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("shipment_id")),
                    ShipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code")),
                    Commodity = reader.IsDBNull(reader.GetOrdinal("commodity")) ? null : reader.GetString(reader.GetOrdinal("commodity")),
                    WeightKg = reader.GetDecimal(reader.GetOrdinal("weight_kg")),
                    VolumeCbm = reader.GetDecimal(reader.GetOrdinal("volume_cbm")),
                    ReceiverName = reader.IsDBNull(reader.GetOrdinal("receiver_name")) ? null : reader.GetString(reader.GetOrdinal("receiver_name")),
                    ReceiverPhone = reader.IsDBNull(reader.GetOrdinal("receiver_phone")) ? null : reader.GetString(reader.GetOrdinal("receiver_phone")),
                    DeliveryAddress = reader.IsDBNull(reader.GetOrdinal("delivery_address")) ? null : reader.GetString(reader.GetOrdinal("delivery_address")),
                    SpecialHandlingNote = reader.IsDBNull(reader.GetOrdinal("special_handling_note")) ? null : reader.GetString(reader.GetOrdinal("special_handling_note")),
                    Status = reader.GetString(reader.GetOrdinal("shipment_status"))
                },
                Trip = new TripInfoDto
                {
                    TripPostId = reader.IsDBNull(reader.GetOrdinal("trip_post_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("trip_post_id")),
                    TripId = reader.IsDBNull(reader.GetOrdinal("trip_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("trip_id")),
                    TripCode = reader.IsDBNull(reader.GetOrdinal("trip_code")) ? null : reader.GetString(reader.GetOrdinal("trip_code")),
                    Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
                    Origin = reader.IsDBNull(reader.GetOrdinal("origin")) ? null : reader.GetString(reader.GetOrdinal("origin")),
                    Destination = reader.IsDBNull(reader.GetOrdinal("destination")) ? null : reader.GetString(reader.GetOrdinal("destination")),
                    DepartureTime = reader.IsDBNull(reader.GetOrdinal("departure_time")) ? null : reader.GetDateTime(reader.GetOrdinal("departure_time")),
                    AcceptUntil = reader.IsDBNull(reader.GetOrdinal("accept_until")) ? null : reader.GetDateTime(reader.GetOrdinal("accept_until")),
                    PickupMode = reader.IsDBNull(reader.GetOrdinal("pickup_mode")) ? null : reader.GetString(reader.GetOrdinal("pickup_mode")),
                    MaxWeight = reader.IsDBNull(reader.GetOrdinal("max_weight")) ? 0m : reader.GetDecimal(reader.GetOrdinal("max_weight")),
                    MaxVolume = reader.IsDBNull(reader.GetOrdinal("max_volume")) ? 0m : reader.GetDecimal(reader.GetOrdinal("max_volume"))
                },
                TripCapacity = new TripCapacityInfoDto
                {
                    CurrentWeight = currentWeight,
                    CurrentVolume = currentVolume,
                    MaxWeight = maxWeight,
                    MaxVolume = maxVolume,
                    RemainingWeight = maxWeight - currentWeight,
                    RemainingVolume = maxVolume - currentVolume
                },
                Customer = new CustomerInfoDto
                {
                    Id = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("customer_id")),
                    FullName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? "N/A" : reader.GetString(reader.GetOrdinal("customer_name")),
                    Phone = reader.IsDBNull(reader.GetOrdinal("customer_phone")) ? null : reader.GetString(reader.GetOrdinal("customer_phone")),
                    Email = reader.IsDBNull(reader.GetOrdinal("customer_email")) ? null : reader.GetString(reader.GetOrdinal("customer_email"))
                }
            };

            await reader.CloseAsync();

            // Get quotations for this proposal
            detail.QuotationHistory = await GetQuotationsForProposalAsync(conn, proposalId, ct);
            detail.CurrentQuotation = detail.QuotationHistory.FirstOrDefault(q => q.Status == "Draft" || q.Status == "Sent");

            // Get audit timeline
            detail.AuditTimeline = await GetAuditTimelineAsync(conn, "Proposal", proposalId, ct);

            return detail;
        }

        public async Task ApproveProposalAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                // 1. Lock and read proposal
                var proposal = await ReadProposalForUpdateAsync(conn, tx, proposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                ProposalTransitionGuard.EnsureCanTransition(
                    Enum.Parse<ProposalStatus>(proposal.Status), ProposalStatus.Approved);

                // 2. Validate shipment
                var shipment = await ReadShipmentForUpdateAsync(conn, tx, proposal.ShipmentId, ct)
                    ?? throw new InvalidOperationException("Shipment không tồn tại.");

                if (shipment.Status != ShipmentStatus.PendingReview.ToString())
                    throw new InvalidOperationException(
                        $"Shipment đang ở trạng thái {shipment.Status}. Chỉ có thể duyệt Proposal khi Shipment PendingReview.");

                // 3. Validate trip
                var tripPost = await ReadTripPostAsync(conn, tx, proposal.TripPostId, ct)
                    ?? throw new InvalidOperationException("Trip Post không tồn tại.");

                var trip = await ReadTripForUpdateAsync(conn, tx, tripPost.TripId, ct)
                    ?? throw new InvalidOperationException("Trip không tồn tại.");

                if (trip.Status == "Completed" || trip.Status == "Cancelled")
                    throw new InvalidOperationException("Trip đã hoàn thành hoặc bị hủy.");

                if (shipment.WeightKg <= 0 || shipment.VolumeCbm <= 0)
                    throw new InvalidOperationException("Weight và Volume phải lớn hơn 0.");

                // 4. Update proposal
                await UpdateProposalStatusAsync(conn, tx, proposalId,
                    ProposalStatusConstants.Approved, staffId, ct);

                // 5. Audit
                await InsertAuditAsync(conn, tx, "Proposal", proposalId, "Approved",
                    "Proposal approved by staff", staffId, ct);

                await tx.CommitAsync(ct);

                // 6. Notification (outside transaction)
                await SaveNotificationAsync(conn, proposal.CustomerId,
                    "Đề xuất đã được duyệt",
                    $"Đề xuất của bạn đã được chấp nhận và đang chờ báo giá.",
                    "Proposal", proposalId, ct);

                // 7. SignalR
                try
                {
                    await _dispatcher.SendProposalStatusToCustomerAsync(proposal.CustomerId, new ProposalEventPayload
                    {
                        EventType = "ProposalApproved",
                        ProposalId = proposalId,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR for proposal approval");
                }

                _logger.LogInformation("Proposal {ProposalId} approved by Staff {StaffId}", proposalId, staffId);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task RejectProposalAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Lý do từ chối là bắt buộc.");

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                // 1. Lock and read proposal
                var proposal = await ReadProposalForUpdateAsync(conn, tx, proposalId, ct)
                    ?? throw new InvalidOperationException("Proposal không tồn tại.");

                ProposalTransitionGuard.EnsureCanTransition(
                    Enum.Parse<ProposalStatus>(proposal.Status), ProposalStatus.Rejected);

                // 2. Update proposal
                await UpdateProposalStatusAsync(conn, tx, proposalId,
                    ProposalStatusConstants.Rejected, staffId, ct, reason);

                // 3. Cancel shipment via state machine (join transaction)
                await _shipmentStateService.TransitionAsync(
                    proposal.ShipmentId,
                    ShipmentStatus.Cancelled,
                    connection: conn,
                    transaction: tx,
                    performedBy: staffId,
                    reason: $"Proposal rejected: {reason}",
                    ct: ct);

                // 4. Audit
                await InsertAuditAsync(conn, tx, "Proposal", proposalId, "Rejected",
                    $"Reason: {reason}", staffId, ct);

                await tx.CommitAsync(ct);

                // 5. Notification
                await SaveNotificationAsync(conn, proposal.CustomerId,
                    "Đề xuất bị từ chối",
                    $"Đề xuất của bạn đã bị từ chối. Lý do: {reason}",
                    "Proposal", proposalId, ct);

                // 6. SignalR
                try
                {
                    await _dispatcher.SendProposalStatusToCustomerAsync(proposal.CustomerId, new ProposalEventPayload
                    {
                        EventType = "ProposalRejected",
                        ProposalId = proposalId,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR for proposal rejection");
                }

                _logger.LogInformation("Proposal {ProposalId} rejected by Staff {StaffId}: {Reason}",
                    proposalId, staffId, reason);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ── Private helpers ──

        private static async Task<(Guid Id, Guid ShipmentId, Guid TripPostId, string Status, DateTime CreatedAt, Guid CustomerId)?>
            ReadProposalForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid proposalId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, shipment_id, trip_post_id, status, created_at, customer_id
                FROM warehouse.shipment_proposals
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDateTime(4), reader.GetGuid(5));
        }

        private static async Task<(Guid Id, string Status, decimal WeightKg, decimal VolumeCbm)?>
            ReadShipmentForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid shipmentId, CancellationToken ct)
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

        private static async Task<(Guid Id, Guid TripId, string? Status)?>
            ReadTripPostAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tripPostId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, trip_id, status FROM transport.trip_posts
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", tripPostId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        }

        private static async Task<(Guid Id, string? Status)?>
            ReadTripForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tripId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, status FROM transport.trips
                WHERE id = @id AND is_deleted = FALSE FOR UPDATE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", tripId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1));
        }

        private static async Task UpdateProposalStatusAsync(
            NpgsqlConnection conn, NpgsqlTransaction tx, Guid proposalId,
            string status, Guid staffId, CancellationToken ct, string? reason = null)
        {
            const string sql = """
                UPDATE warehouse.shipment_proposals
                SET status = @status,
                    reviewer_id = @staff_id,
                    reviewer_name = (SELECT full_name FROM identity.users WHERE id = @staff_id),
                    reviewed_at = NOW(),
                    approved_at = CASE WHEN @status = 'Approved' THEN NOW() ELSE approved_at END,
                    rejected_at = CASE WHEN @status = 'Rejected' THEN NOW() ELSE rejected_at END,
                    reject_reason = @reject_reason
                WHERE id = @id AND is_deleted = FALSE;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("id", proposalId);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("staff_id", staffId);
            cmd.Parameters.AddWithValue("reject_reason", (object)reason! ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
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

        private static async Task<List<QuotationSummaryDto>> GetQuotationsForProposalAsync(
            NpgsqlConnection conn, Guid proposalId, CancellationToken ct)
        {
            const string sql = """
                SELECT id, quotation_code, shipping_fee, deposit_amount, currency, status,
                       sent_at, expires_at, accepted_at, created_at
                FROM warehouse.quotations
                WHERE proposal_id = @proposal_id AND is_deleted = FALSE
                ORDER BY created_at DESC;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var list = new List<QuotationSummaryDto>();
            while (await reader.ReadAsync())
            {
                var shippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee"));
                var depositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount"));
                list.Add(new QuotationSummaryDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    QuotationCode = reader.IsDBNull(reader.GetOrdinal("quotation_code")) ? null : reader.GetString(reader.GetOrdinal("quotation_code")),
                    ShippingFee = shippingFee,
                    DepositAmount = depositAmount,
                    RemainingAmount = shippingFee - depositAmount,
                    Currency = reader.GetString(reader.GetOrdinal("currency")),
                    Status = reader.GetString(reader.GetOrdinal("status")),
                    SentAt = reader.IsDBNull(reader.GetOrdinal("sent_at")) ? null : reader.GetDateTime(reader.GetOrdinal("sent_at")),
                    ExpiresAt = reader.IsDBNull(reader.GetOrdinal("expires_at")) ? null : reader.GetDateTime(reader.GetOrdinal("expires_at")),
                    AcceptedAt = reader.IsDBNull(reader.GetOrdinal("accepted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("accepted_at")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }
            return list;
        }

        private static async Task<List<ProposalAuditEntry>> GetAuditTimelineAsync(
            NpgsqlConnection conn, string entityType, Guid entityId, CancellationToken ct)
        {
            const string sql = """
                SELECT al.action, al.performed_by, al.details, al.created_at,
                       u.full_name AS performed_by_name
                FROM shared.audit_log al
                LEFT JOIN identity.users u ON u.id = al.performed_by
                WHERE al.entity_type = @entity_type AND al.entity_id = @entity_id
                ORDER BY al.created_at DESC;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("entity_type", entityType);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var list = new List<ProposalAuditEntry>();
            while (await reader.ReadAsync())
            {
                list.Add(new ProposalAuditEntry
                {
                    Action = reader.GetString(reader.GetOrdinal("action")),
                    PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetGuid(reader.GetOrdinal("performed_by")),
                    PerformedByName = reader.IsDBNull(reader.GetOrdinal("performed_by_name")) ? null : reader.GetString(reader.GetOrdinal("performed_by_name")),
                    Details = reader.IsDBNull(reader.GetOrdinal("details")) ? null : reader.GetString(reader.GetOrdinal("details")),
                    OccurredAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }
            return list;
        }

        public async Task<PagedResult<StaffQuotationListItem>> GetQuotationsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, int page, int pageSize,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var whereClauses = new List<string> { "q.is_deleted = FALSE" };
            var parameters = new List<NpgsqlParameter>();

            // Hub filtering: Staff only sees quotations for proposals originating from their hub
            if (role == "Warehouse_Staff" && hubId.HasValue)
            {
                whereClauses.Add("tp.created_by_staff_hub = @hubId");
                parameters.Add(new NpgsqlParameter("hubId", hubId.Value));
            }

            if (!string.IsNullOrEmpty(status))
            {
                whereClauses.Add("q.status = @status");
                parameters.Add(new NpgsqlParameter("status", status));
            }

            var whereSql = string.Join(" AND ", whereClauses);

            // Count
            var countSql = $"""
                SELECT COUNT(*)
                FROM warehouse.quotations q
                LEFT JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id AND sp.is_deleted = FALSE
                LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                WHERE {whereSql};
            """;
            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                var result = await countCmd.ExecuteScalarAsync(ct);
                var totalCount = Convert.ToInt32(result ?? 0);

                var offset = (page - 1) * pageSize;
                var querySql = $"""
                    SELECT
                        q.id,
                        q.quotation_code,
                        q.proposal_id,
                        sp.id AS shipment_id,
                        s.shipment_code,
                        c.full_name AS customer_name,
                        q.shipping_fee,
                        q.deposit_amount,
                        q.currency,
                        q.status,
                        q.sent_at,
                        q.expires_at,
                        q.created_at
                    FROM warehouse.quotations q
                    LEFT JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id AND sp.is_deleted = FALSE
                    LEFT JOIN warehouse.shipments s ON s.id = sp.shipment_id AND s.is_deleted = FALSE
                    LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                    LEFT JOIN identity.users c ON c.id = sp.customer_id AND c.is_deleted = FALSE
                    WHERE {whereSql}
                    ORDER BY q.created_at DESC
                    LIMIT @limit OFFSET @offset;
                """;

                var allParams = parameters.ToList();
                allParams.Add(new NpgsqlParameter("limit", pageSize));
                allParams.Add(new NpgsqlParameter("offset", offset));

                await using var cmd = new NpgsqlCommand(querySql, conn);
                cmd.Parameters.AddRange(allParams.ToArray());

                var items = new List<StaffQuotationListItem>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync())
                {
                    items.Add(new StaffQuotationListItem
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        QuotationCode = reader.IsDBNull(reader.GetOrdinal("quotation_code")) ? null : reader.GetString(reader.GetOrdinal("quotation_code")),
                        ProposalId = reader.GetGuid(reader.GetOrdinal("proposal_id")),
                        ShipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code")),
                        CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
                        ShippingFee = reader.GetDecimal(reader.GetOrdinal("shipping_fee")),
                        DepositAmount = reader.GetDecimal(reader.GetOrdinal("deposit_amount")),
                        Currency = reader.GetString(reader.GetOrdinal("currency")),
                        Status = reader.GetString(reader.GetOrdinal("status")),
                        SentAt = reader.IsDBNull(reader.GetOrdinal("sent_at")) ? null : reader.GetDateTime(reader.GetOrdinal("sent_at")),
                        ExpiresAt = reader.IsDBNull(reader.GetOrdinal("expires_at")) ? null : reader.GetDateTime(reader.GetOrdinal("expires_at")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    });
                }

                return new PagedResult<StaffQuotationListItem>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
        }

        public async Task<PagedResult<StaffPaymentListItem>> GetPaymentsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, int page, int pageSize,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var whereClauses = new List<string> { "p.is_deleted = FALSE" };
            var parameters = new List<NpgsqlParameter>();

            // Hub filtering: Staff only sees payments for their hub's shipments/proposals
            if (role == "Warehouse_Staff" && hubId.HasValue)
            {
                whereClauses.Add("tp.created_by_staff_hub = @hubId");
                parameters.Add(new NpgsqlParameter("hubId", hubId.Value));
            }

            if (!string.IsNullOrEmpty(status))
            {
                whereClauses.Add("p.status = @status");
                parameters.Add(new NpgsqlParameter("status", status));
            }

            var whereSql = string.Join(" AND ", whereClauses);

            // Count
            var countSql = $"""
                SELECT COUNT(*)
                FROM warehouse.payments p
                LEFT JOIN warehouse.quotations q ON q.id = p.quotation_id AND q.is_deleted = FALSE
                LEFT JOIN warehouse.shipment_proposals sp ON sp.id = q.proposal_id AND sp.is_deleted = FALSE
                LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
                WHERE {whereSql};
            """;
            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                var result = await countCmd.ExecuteScalarAsync(ct);
                var totalCount = Convert.ToInt32(result ?? 0);

                var offset = (page - 1) * pageSize;
                var querySql = $"""
                    SELECT
                        p.id,
                        p.payment_code,
                        p.quotation_id,
                        q.quotation_code,
                        p.shipment_id,
                        s.shipment_code,
                        cu.full_name AS customer_name,
                        p.payment_type,
                        p.amount,
                        p.currency,
                        p.status,
                        p.transaction_reference,
                        p.paid_at,
                        p.created_at
                    FROM warehouse.payments p
                    LEFT JOIN warehouse.quotations q ON q.id = p.quotation_id AND q.is_deleted = FALSE
                    LEFT JOIN warehouse.shipments s ON s.id = p.shipment_id AND s.is_deleted = FALSE
                    LEFT JOIN transport.trip_posts tp ON tp.id = (SELECT sp2.trip_post_id FROM warehouse.shipment_proposals sp2 WHERE sp2.id = q.proposal_id) AND tp.is_deleted = FALSE
                    LEFT JOIN identity.users cu ON cu.id = p.customer_id AND cu.is_deleted = FALSE
                    WHERE {whereSql}
                    ORDER BY p.created_at DESC
                    LIMIT @limit OFFSET @offset;
                """;

                var allParams = parameters.ToList();
                allParams.Add(new NpgsqlParameter("limit", pageSize));
                allParams.Add(new NpgsqlParameter("offset", offset));

                await using var cmd = new NpgsqlCommand(querySql, conn);
                cmd.Parameters.AddRange(allParams.ToArray());

                var items = new List<StaffPaymentListItem>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync())
                {
                    items.Add(new StaffPaymentListItem
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        PaymentCode = reader.IsDBNull(reader.GetOrdinal("payment_code")) ? null : reader.GetString(reader.GetOrdinal("payment_code")),
                        QuotationId = reader.GetGuid(reader.GetOrdinal("quotation_id")),
                        QuotationCode = reader.IsDBNull(reader.GetOrdinal("quotation_code")) ? null : reader.GetString(reader.GetOrdinal("quotation_code")),
                        ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id")),
                        ShipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code")),
                        CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
                        PaymentType = reader.GetString(reader.GetOrdinal("payment_type")),
                        Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                        Currency = reader.GetString(reader.GetOrdinal("currency")),
                        Status = reader.GetString(reader.GetOrdinal("status")),
                        TransactionReference = reader.IsDBNull(reader.GetOrdinal("transaction_reference")) ? null : reader.GetString(reader.GetOrdinal("transaction_reference")),
                        PaidAt = reader.IsDBNull(reader.GetOrdinal("paid_at")) ? null : reader.GetDateTime(reader.GetOrdinal("paid_at")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    });
                }

                return new PagedResult<StaffPaymentListItem>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
        }
    }
}
