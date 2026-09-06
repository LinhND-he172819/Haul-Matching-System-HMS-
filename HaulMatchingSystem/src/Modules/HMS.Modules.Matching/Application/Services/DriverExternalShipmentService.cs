using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Matching.Application.Services
{
    /// <summary>
    /// Service for Driver External Shipment Declaration feature.
    /// Allows drivers to declare shipments encountered outside the system.
    /// Creates a Shipment (Draft) + ShipmentProposal (source=Driver, PendingReview) in a single transaction.
    /// </summary>
    public class DriverExternalShipmentService : IDriverExternalShipmentService
    {
        private readonly string _connStr;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<DriverExternalShipmentService> _logger;

        public DriverExternalShipmentService(
            IConfiguration configuration,
            IShipmentStateService shipmentStateService,
            IRealtimeDispatcher dispatcher,
            ILogger<DriverExternalShipmentService> logger)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
            _shipmentStateService = shipmentStateService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task<CreateExternalShipmentResponse> CreateExternalShipmentAsync(
            Guid driverId, CreateExternalShipmentRequest request, CancellationToken ct)
        {
            // ── 1. Validate driver has an active trip ──
            var activeTrip = await FindDriverActiveTripAsync(driverId, ct)
                ?? throw new InvalidOperationException(
                    "Bạn chưa có chuyến đang hoạt động. Vui lòng bắt đầu chuyến trước khi khai báo hàng ngoài.");

            // ── 2. Validate weight/volume > 0 ──
            if (request.WeightKg <= 0)
                throw new InvalidOperationException("Trọng lượng phải lớn hơn 0.");
            if (request.VolumeCbm <= 0)
                throw new InvalidOperationException("Thể tích phải lớn hơn 0.");

            // ── 3. Validate required fields ──
            if (string.IsNullOrWhiteSpace(request.SenderName))
                throw new InvalidOperationException("Tên người gửi không được để trống.");
            if (string.IsNullOrWhiteSpace(request.SenderPhone))
                throw new InvalidOperationException("Số điện thoại người gửi không được để trống.");
            if (string.IsNullOrWhiteSpace(request.PickupAddress))
                throw new InvalidOperationException("Địa chỉ lấy hàng không được để trống.");

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                // ── 4. Generate shipment code ──
                var shipmentCode = $"GC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

                // ── 5. Create Shipment in warehouse.shipments ──
                var shipmentId = Guid.NewGuid();
                const string insertShipmentSql = """
                    INSERT INTO warehouse.shipments (
                        id, qr_code,
                        customer_id,
                        sender_name, sender_phone,
                        pickup_address,
                        receiver_name, receiver_phone,
                        dest_address,
                        cargo_type,
                        weight_kg, volume_cbm,
                        special_handling_note,
                        status,
                        created_at, updated_at, is_deleted
                    ) VALUES (
                        @id, @qr_code,
                        NULL,
                        @sender_name, @sender_phone,
                        @pickup_address,
                        @receiver_name, @receiver_phone,
                        @dest_address,
                        @cargo_type,
                        @weight_kg, @volume_cbm,
                        @special_handling_note,
                        'Draft',
                        NOW(), NOW(), FALSE
                    );
                """;
                await using (var shipmentCmd = new NpgsqlCommand(insertShipmentSql, conn, tx))
                {
                    shipmentCmd.Parameters.AddWithValue("id", shipmentId);
                    shipmentCmd.Parameters.AddWithValue("qr_code", shipmentCode);
                    shipmentCmd.Parameters.AddWithValue("sender_name", request.SenderName);
                    shipmentCmd.Parameters.AddWithValue("sender_phone", request.SenderPhone);
                    shipmentCmd.Parameters.AddWithValue("pickup_address", request.PickupAddress);
                    shipmentCmd.Parameters.AddWithValue("receiver_name", request.ReceiverName);
                    shipmentCmd.Parameters.AddWithValue("receiver_phone", request.ReceiverPhone);
                    shipmentCmd.Parameters.AddWithValue("dest_address", request.DestAddress);
                    shipmentCmd.Parameters.AddWithValue("cargo_type", request.Category);
                    shipmentCmd.Parameters.AddWithValue("weight_kg", request.WeightKg);
                    shipmentCmd.Parameters.AddWithValue("volume_cbm", request.VolumeCbm);
                    shipmentCmd.Parameters.AddWithValue("special_handling_note", (object?)request.SpecialHandlingNote ?? DBNull.Value);
                    await shipmentCmd.ExecuteNonQueryAsync(ct);
                }

                // ── 6. Create ShipmentProposal (source=Driver) ──
                var proposalId = Guid.NewGuid();
                const string insertProposalSql = """
                    INSERT INTO warehouse.shipment_proposals (
                        id, shipment_id,
                        trip_post_id, customer_id,
                        proposal_source, driver_id, requested_trip_id,
                        sender_name, sender_phone, pickup_address,
                        status,
                        created_at, is_deleted
                    ) VALUES (
                        @id, @shipment_id,
                        NULL, NULL,
                        'Driver', @driver_id, @requested_trip_id,
                        @sender_name, @sender_phone, @pickup_address,
                        'PendingReview',
                        NOW(), FALSE
                    );
                """;
                await using (var proposalCmd = new NpgsqlCommand(insertProposalSql, conn, tx))
                {
                    proposalCmd.Parameters.AddWithValue("id", proposalId);
                    proposalCmd.Parameters.AddWithValue("shipment_id", shipmentId);
                    proposalCmd.Parameters.AddWithValue("driver_id", driverId);
                    proposalCmd.Parameters.AddWithValue("requested_trip_id", activeTrip.TripId);
                    proposalCmd.Parameters.AddWithValue("sender_name", request.SenderName);
                    proposalCmd.Parameters.AddWithValue("sender_phone", request.SenderPhone);
                    proposalCmd.Parameters.AddWithValue("pickup_address", request.PickupAddress);
                    await proposalCmd.ExecuteNonQueryAsync(ct);
                }

                // ── 7. Audit ──
                const string auditSql = """
                    INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                    VALUES ('Proposal', @entity_id, 'Created', @performed_by, @details::jsonb, NOW());
                """;
                await using (var auditCmd = new NpgsqlCommand(auditSql, conn, tx))
                {
                    auditCmd.Parameters.AddWithValue("entity_id", proposalId);
                    auditCmd.Parameters.AddWithValue("performed_by", driverId);
                    auditCmd.Parameters.AddWithValue("details",
                        $"{{\"message\": \"Driver declared external shipment: {shipmentCode}\", \"shipmentId\": \"{shipmentId}\"}}");
                    await auditCmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);

                // ── 8. Send SignalR notification to staff (outside transaction) ──
                try
                {
                    // Notify all staff/admin about the new external shipment proposal
                    await _dispatcher.SendProposalStatusToCustomerAsync(driverId, new ProposalEventPayload
                    {
                        EventType = "NewExternalShipmentProposal",
                        ProposalId = proposalId,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR notification for external shipment proposal");
                }

                _logger.LogInformation(
                    "External shipment created by Driver {DriverId}: Shipment {ShipmentId}, Proposal {ProposalId}, Code {ShipmentCode}",
                    driverId, shipmentId, proposalId, shipmentCode);

                return new CreateExternalShipmentResponse
                {
                    ShipmentId = shipmentId,
                    ProposalId = proposalId,
                    RequestedTripId = activeTrip.TripId,
                    ShipmentCode = shipmentCode,
                    ProposalStatus = "PendingReview",
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<PagedResult<ExternalShipmentListItem>> GetExternalShipmentsAsync(
            Guid driverId, string? status, int page, int pageSize, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var whereClauses = new List<string>
            {
                "sp.proposal_source = 'Driver'",
                "sp.driver_id = @driver_id",
                "sp.is_deleted = FALSE"
            };
            var parameters = new List<NpgsqlParameter>
            {
                new("driver_id", driverId)
            };

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
                WHERE {whereSql};
            """;
            int totalCount = 0;
            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                foreach (var p in parameters) countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                var result = await countCmd.ExecuteScalarAsync(ct);
                totalCount = Convert.ToInt32(result ?? 0);
            }

            // Paged query (fresh parameters to avoid Npgsql "parameter already belongs" error)
            {
                var offset = (page - 1) * pageSize;
                var querySql = $"""
                    SELECT
                        sp.id AS proposal_id,
                        sp.status AS proposal_status,
                        sp.created_at,
                        s.id AS shipment_id,
                        s.qr_code AS shipment_code,
                        s.cargo_type AS category,
                        s.weight_kg,
                        s.volume_cbm,
                        s.receiver_name,
                        s.dest_address,
                        s.status AS shipment_status,
                        q.status AS quotation_status
                    FROM warehouse.shipment_proposals sp
                    JOIN warehouse.shipments s ON s.id = sp.shipment_id AND s.is_deleted = FALSE
                    LEFT JOIN warehouse.quotations q ON q.proposal_id = sp.id AND q.is_deleted = FALSE
                        AND q.status NOT IN ('Cancelled', 'Expired')
                    WHERE {whereSql}
                    ORDER BY sp.created_at DESC
                    LIMIT @limit OFFSET @offset;
                """;

                var allParams = new List<NpgsqlParameter>();
                foreach (var p in parameters) allParams.Add(new NpgsqlParameter(p.ParameterName, p.Value));
                allParams.Add(new NpgsqlParameter("limit", pageSize));
                allParams.Add(new NpgsqlParameter("offset", offset));

                await using var cmd = new NpgsqlCommand(querySql, conn);
                cmd.Parameters.AddRange(allParams.ToArray());

                var items = new List<ExternalShipmentListItem>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync())
                {
                    items.Add(new ExternalShipmentListItem
                    {
                        ProposalId = reader.GetGuid(reader.GetOrdinal("proposal_id")),
                        ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id")),
                        ShipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code")),
                        ProposalStatus = reader.GetString(reader.GetOrdinal("proposal_status")),
                        Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "N/A" : reader.GetString(reader.GetOrdinal("category")),
                        WeightKg = reader.GetDecimal(reader.GetOrdinal("weight_kg")),
                        VolumeCbm = reader.GetDecimal(reader.GetOrdinal("volume_cbm")),
                        ReceiverName = reader.IsDBNull(reader.GetOrdinal("receiver_name")) ? "N/A" : reader.GetString(reader.GetOrdinal("receiver_name")),
                        DestAddress = reader.IsDBNull(reader.GetOrdinal("dest_address")) ? null : reader.GetString(reader.GetOrdinal("dest_address")),
                        ShipmentStatus = reader.GetString(reader.GetOrdinal("shipment_status")),
                        QuotationStatus = reader.IsDBNull(reader.GetOrdinal("quotation_status")) ? null : reader.GetString(reader.GetOrdinal("quotation_status")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    });
                }

                return new PagedResult<ExternalShipmentListItem>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
        }

        public async Task<ExternalShipmentDetail?> GetExternalShipmentDetailAsync(
            Guid driverId, Guid proposalId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT
                    sp.id AS proposal_id,
                    sp.status AS proposal_status,
                    sp.created_at,
                    s.id AS shipment_id,
                    s.qr_code AS shipment_code,
                    s.sender_name, s.sender_phone, s.pickup_address,
                    s.receiver_name, s.receiver_phone, s.dest_address,
                    s.cargo_type AS category,
                    s.weight_kg, s.volume_cbm,
                    s.special_handling_note,
                    s.status AS shipment_status,
                    s.cod_amount,
                    t.id AS trip_id, t.trip_code,
                    oh.name AS origin, dh.name AS destination,
                    q.shipping_fee, q.deposit_amount,
                    q.status AS quotation_status
                FROM warehouse.shipment_proposals sp
                JOIN warehouse.shipments s ON s.id = sp.shipment_id AND s.is_deleted = FALSE
                LEFT JOIN transport.trips t ON t.id = sp.requested_trip_id AND t.is_deleted = FALSE
                LEFT JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
                LEFT JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
                LEFT JOIN warehouse.quotations q ON q.proposal_id = sp.id AND q.is_deleted = FALSE
                    AND q.status NOT IN ('Cancelled', 'Expired')
                WHERE sp.id = @proposal_id
                    AND sp.driver_id = @driver_id
                    AND sp.proposal_source = 'Driver'
                    AND sp.is_deleted = FALSE;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync()) return null;

            // Read all scalar values first, then close reader before building timeline
            var proposalId2 = reader.GetGuid(reader.GetOrdinal("proposal_id"));
            var shipmentId2 = reader.GetGuid(reader.GetOrdinal("shipment_id"));
            var shipmentCode = reader.IsDBNull(reader.GetOrdinal("shipment_code")) ? null : reader.GetString(reader.GetOrdinal("shipment_code"));
            var senderName = reader.IsDBNull(reader.GetOrdinal("sender_name")) ? "N/A" : reader.GetString(reader.GetOrdinal("sender_name"));
            var senderPhone = reader.IsDBNull(reader.GetOrdinal("sender_phone")) ? "N/A" : reader.GetString(reader.GetOrdinal("sender_phone"));
            var pickupAddress = reader.IsDBNull(reader.GetOrdinal("pickup_address")) ? "N/A" : reader.GetString(reader.GetOrdinal("pickup_address"));
            var receiverName = reader.IsDBNull(reader.GetOrdinal("receiver_name")) ? "N/A" : reader.GetString(reader.GetOrdinal("receiver_name"));
            var receiverPhone = reader.IsDBNull(reader.GetOrdinal("receiver_phone")) ? "N/A" : reader.GetString(reader.GetOrdinal("receiver_phone"));
            var destAddress = reader.IsDBNull(reader.GetOrdinal("dest_address")) ? "N/A" : reader.GetString(reader.GetOrdinal("dest_address"));
            var category = reader.IsDBNull(reader.GetOrdinal("category")) ? "N/A" : reader.GetString(reader.GetOrdinal("category"));
            var weightKg = reader.GetDecimal(reader.GetOrdinal("weight_kg"));
            var volumeCbm = reader.GetDecimal(reader.GetOrdinal("volume_cbm"));
            var codAmountRaw = reader.IsDBNull(reader.GetOrdinal("cod_amount")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("cod_amount"));
            var note = reader.IsDBNull(reader.GetOrdinal("special_handling_note")) ? null : reader.GetString(reader.GetOrdinal("special_handling_note"));
            var proposalStatus = reader.GetString(reader.GetOrdinal("proposal_status"));
            var shipmentStatus = reader.GetString(reader.GetOrdinal("shipment_status"));
            var quotationStatus = reader.IsDBNull(reader.GetOrdinal("quotation_status")) ? null : reader.GetString(reader.GetOrdinal("quotation_status"));
            var shippingFee = reader.IsDBNull(reader.GetOrdinal("shipping_fee")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("shipping_fee"));
            var depositAmount = reader.IsDBNull(reader.GetOrdinal("deposit_amount")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("deposit_amount"));
            var tripId = reader.IsDBNull(reader.GetOrdinal("trip_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("trip_id"));
            var tripCode = reader.IsDBNull(reader.GetOrdinal("trip_code")) ? null : reader.GetString(reader.GetOrdinal("trip_code"));
            var origin = reader.IsDBNull(reader.GetOrdinal("origin")) ? null : reader.GetString(reader.GetOrdinal("origin"));
            var destination = reader.IsDBNull(reader.GetOrdinal("destination")) ? null : reader.GetString(reader.GetOrdinal("destination"));
            var createdAt = reader.GetDateTime(reader.GetOrdinal("created_at"));

            await reader.CloseAsync();

            // ── Build timeline (separate query, after closing previous reader) ──
            var timeline = await GetExternalShipmentTimelineAsync(conn, proposalId2, ct);

            return new ExternalShipmentDetail
            {
                ProposalId = proposalId2,
                ShipmentId = shipmentId2,
                ShipmentCode = shipmentCode,
                SenderName = senderName,
                SenderPhone = senderPhone,
                PickupAddress = pickupAddress,
                ReceiverName = receiverName,
                ReceiverPhone = receiverPhone,
                DestAddress = destAddress,
                Category = category,
                WeightKg = weightKg,
                VolumeCbm = volumeCbm,
                SpecialHandlingNote = note,
                CodAmount = codAmountRaw ?? 0m,
                ProposalStatus = proposalStatus,
                ShipmentStatus = shipmentStatus,
                QuotationStatus = quotationStatus,
                ShippingFee = shippingFee,
                DepositAmount = depositAmount,
                TripId = tripId,
                TripCode = tripCode,
                Origin = origin,
                Destination = destination,
                CreatedAt = createdAt,
                Timeline = timeline
            };
        }

        // ── Private helpers ──

        private async Task<(Guid TripId, string? Status)?> FindDriverActiveTripAsync(Guid driverId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT t.id, t.status
                FROM transport.trips t
                WHERE t.driver_id = @driver_id
                    AND t.is_deleted = FALSE
                    AND t.status IN ('InProgress', 'Active', 'Scheduled')
                ORDER BY t.created_at DESC
                LIMIT 1;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetString(1));
        }

        private static async Task<List<ExternalShipmentTimelineEntry>> GetExternalShipmentTimelineAsync(
            NpgsqlConnection conn, Guid proposalId, CancellationToken ct)
        {
            const string sql = """
                SELECT al.action, al.details, al.created_at
                FROM shared.audit_log al
                WHERE al.entity_type = 'Proposal' AND al.entity_id = @proposal_id
                ORDER BY al.created_at ASC;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var timeline = new List<ExternalShipmentTimelineEntry>();
            var statusFlow = new[] { "Created", "PendingReview", "Approved", "Quoted", "Deposited", "Matched", "In_Warehouse", "In_Transit", "Delivered", "Completed" };

            while (await reader.ReadAsync())
            {
                var action = reader.GetString(reader.GetOrdinal("action"));
                var details = reader.IsDBNull(reader.GetOrdinal("details")) ? null : reader.GetString(reader.GetOrdinal("details"));
                var occurredAt = reader.GetDateTime(reader.GetOrdinal("created_at"));

                var flowIndex = Array.IndexOf(statusFlow, action);
                var currentActionIndex = timeline.Count > 0
                    ? Array.IndexOf(statusFlow, timeline.Last().Action)
                    : -1;

                timeline.Add(new ExternalShipmentTimelineEntry
                {
                    Action = action,
                    Details = details,
                    OccurredAt = occurredAt,
                    IsCompleted = true,
                    IsCurrent = flowIndex > currentActionIndex
                });
            }

            return timeline;
        }
    }
}
