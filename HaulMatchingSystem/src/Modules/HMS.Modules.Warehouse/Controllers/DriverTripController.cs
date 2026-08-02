using System.Security.Claims;
using HMS.Modules.Transport.Core.StateMachines;
using HMS.Modules.Warehouse.Application.DTOs.Driver;
using HMS.Shared.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/driver")]
[Authorize(Roles = "Driver")]
public class DriverTripController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public DriverTripController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        return userId;
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection") ?? "";

    // ─── GET /api/driver/trips ─────────────────────────────────────────
    [HttpGet("trips")]
    public async Task<IActionResult> GetMyTrips(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT t.id, t.trip_code, oh.name AS origin_name, dh.name AS destination_name,
                   t.started_at AS departure_time, v.license_plate, t.status,
                   t.current_load_weight, t.current_load_volume,
                   v.max_weight_kg, v.max_volume_cbm,
                   (SELECT COUNT(*) FROM transport.trip_shipments ts
                    WHERE ts.trip_id = t.id AND ts.is_deleted = FALSE) AS total_shipments
            FROM transport.trips t
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            WHERE t.driver_id = @driver_id AND t.is_deleted = FALSE
            ORDER BY
                CASE t.status
                    WHEN 'InProgress' THEN 1
                    WHEN 'Ready' THEN 2
                    WHEN 'Scheduled' THEN 3
                    WHEN 'Completed' THEN 4
                    WHEN 'Cancelled' THEN 5
                    WHEN 'Breakdown' THEN 6
                    WHEN 'Active' THEN 7
                    ELSE 10
                END,
                t.created_at DESC
            OFFSET @offset LIMIT @limit;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("driver_id", driverId);
        cmd.Parameters.AddWithValue("offset", Math.Max(0, (page - 1) * pageSize));
        cmd.Parameters.AddWithValue("limit", pageSize);

        var items = new List<DriverTripListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = reader.GetString(6);
            var maxWeight = reader.GetDecimal(9);
            var maxVolume = reader.GetDecimal(10);
            var currentWeight = reader.GetDecimal(7);
            var currentVolume = reader.GetDecimal(8);

            items.Add(new DriverTripListItem
            {
                Id = reader.GetGuid(0),
                TripCode = reader.IsDBNull(1) ? $"TRIP-{reader.GetGuid(0).ToString()[..8]}" : reader.GetString(1),
                OriginName = reader.GetString(2),
                DestinationName = reader.GetString(3),
                DepartureTime = reader.IsDBNull(4) ? (DateTimeOffset?)null : reader.GetDateTime(4),
                VehiclePlate = reader.GetString(5),
                Status = status,
                TotalShipments = reader.GetInt32(11),
                CurrentWeight = currentWeight,
                RemainingWeight = maxWeight - currentWeight,
                CurrentVolume = currentVolume,
                RemainingVolume = maxVolume - currentVolume,
                AllowedActions = ComputeTripAllowedActions(status)
            });
        }

        return Ok(new { items });
    }

    // ─── GET /api/driver/trips/{tripId} ────────────────────────────────
    [HttpGet("trips/{tripId:guid}")]
    public async Task<IActionResult> GetTripDetail(Guid tripId, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT t.id, t.trip_code, t.status, t.started_at, t.completed_at,
                   v.license_plate, oh.name AS origin_name, dh.name AS destination_name,
                   ST_AsText(t.route_linestring) AS route_wkt,
                   t.current_load_weight, t.current_load_volume,
                   v.max_weight_kg, v.max_volume_cbm
            FROM transport.trips t
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            WHERE t.id = @trip_id AND t.driver_id = @driver_id AND t.is_deleted = FALSE;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("trip_id", tripId);
        cmd.Parameters.AddWithValue("driver_id", driverId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy chuyến đi hoặc bạn không có quyền truy cập." });

        var status = reader.GetString(2);
        var maxWeight = reader.GetDecimal(11);
        var maxVolume = reader.GetDecimal(12);
        var currentWeight = reader.GetDecimal(9);
        var currentVolume = reader.GetDecimal(10);

        var detail = new DriverTripDetail
        {
            Id = reader.GetGuid(0),
            TripCode = reader.IsDBNull(1) ? $"TRIP-{reader.GetGuid(0).ToString()[..8]}" : reader.GetString(1),
            Status = status,
            DepartureTime = reader.IsDBNull(3) ? (DateTimeOffset?)null : reader.GetDateTime(3),
            VehiclePlate = reader.GetString(5),
            OriginName = reader.GetString(6),
            DestinationName = reader.GetString(7),
            RouteLineString = reader.IsDBNull(8) ? null : reader.GetString(8),
            CurrentWeight = currentWeight,
            RemainingWeight = maxWeight - currentWeight,
            MaxWeight = maxWeight,
            CurrentVolume = currentVolume,
            RemainingVolume = maxVolume - currentVolume,
            MaxVolume = maxVolume,
            AllowedActions = ComputeTripAllowedActions(status)
        };

        await reader.CloseAsync();

        // Load shipments in this trip
        detail = detail with { Shipments = await LoadTripShipments(conn, tripId, ct) };
        detail = detail with { TotalShipments = detail.Shipments.Count };

        // Load timeline
        detail = detail with { Timeline = await LoadTripTimeline(conn, tripId, ct) };

        return Ok(detail);
    }

    // ─── GET /api/driver/trips/{tripId}/shipments/{shipmentId} ─────────
    [HttpGet("trips/{tripId:guid}/shipments/{shipmentId:guid}")]
    public async Task<IActionResult> GetShipmentDetail(Guid tripId, Guid shipmentId, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify trip belongs to driver
        const string verifyTrip = """
            SELECT id FROM transport.trips
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(verifyTrip, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            if (await cmd.ExecuteScalarAsync(ct) == null)
                return Forbid();
        }

        // Verify shipment belongs to trip
        const string verifyShipment = """
            SELECT ts.id FROM transport.trip_shipments ts
            WHERE ts.trip_id = @trip_id AND ts.shipment_id = @shipment_id AND ts.is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(verifyShipment, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            if (await cmd.ExecuteScalarAsync(ct) == null)
                return NotFound(new { message = "Không tìm thấy kiện hàng trong chuyến đi." });
        }

        // Get shipment detail
        const string sql = """
            SELECT s.id, s.qr_code, s.status, s.cargo_type, s.weight_kg, s.volume_cbm,
                   s.special_handling_note,
                   s.sender_name, s.sender_phone, s.pickup_address, s.pickup_note,
                   s.receiver_name, s.receiver_phone, s.dest_address
            FROM warehouse.shipments s
            WHERE s.id = @shipment_id AND s.is_deleted = FALSE;
        """;

        await using var cmd2 = new NpgsqlCommand(sql, conn);
        cmd2.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd2.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy kiện hàng." });

        var status = reader.GetString(2);
        var detail = new DriverShipmentDetail
        {
            Id = reader.GetGuid(0),
            ShipmentCode = reader.GetString(1),
            Status = status,
            Commodity = reader.IsDBNull(3) ? null : reader.GetString(3),
            Weight = reader.GetDecimal(4),
            Volume = reader.GetDecimal(5),
            SpecialInstructions = reader.IsDBNull(6) ? null : reader.GetString(6),
            SenderName = reader.IsDBNull(7) ? null : reader.GetString(7),
            SenderPhone = reader.IsDBNull(8) ? null : reader.GetString(8),
            PickupAddress = reader.IsDBNull(9) ? null : reader.GetString(9),
            PickupNote = reader.IsDBNull(10) ? null : reader.GetString(10),
            ReceiverName = reader.IsDBNull(11) ? null : reader.GetString(11),
            ReceiverPhone = reader.IsDBNull(12) ? null : reader.GetString(12),
            DeliveryAddress = reader.IsDBNull(13) ? null : reader.GetString(13),
            AllowedActions = ComputeDriverShipmentAllowedActions(status)
        };

        await reader.CloseAsync();

        // Load timeline
        detail = detail with { Timeline = await LoadShipmentTimeline(conn, shipmentId, ct) };

        // Populate pending COD payment if Delivered
        if (status == "Delivered")
        {
            const string codSql = """
                SELECT id, amount, currency, payment_code FROM warehouse.payments
                WHERE shipment_id = @shipment_id
                  AND payment_type = 'FinalPayment' AND payment_method = 'COD' AND status = 'Pending'
                  AND is_deleted = FALSE
                LIMIT 1;
            """;
            await using var codCmd = new NpgsqlCommand(codSql, conn);
            codCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            await using var codReader = await codCmd.ExecuteReaderAsync(ct);
            if (await codReader.ReadAsync(ct))
            {
                var codPaymentId = codReader.GetGuid(0);
                var codAmount = codReader.GetDecimal(1);
                var codCurrency = codReader.GetString(2);
                var codPaymentCode = codReader.IsDBNull(3) ? null : codReader.GetString(3);
                detail = detail with
                {
                    PendingCodPaymentId = codPaymentId,
                    PendingCodAmount = codAmount,
                    PendingCodCurrency = codCurrency,
                    PendingCodPaymentCode = codPaymentCode,
                    AllowedActions = detail.AllowedActions with { CanConfirmCod = true }
                };
            }
        }

        return Ok(detail);
    }

    // ─── PUT /api/driver/trips/{tripId}/start ──────────────────────────
    [HttpPut("trips/{tripId:guid}/start")]
    public async Task<IActionResult> StartTrip(Guid tripId, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify ownership and status
        const string checkSql = """
            SELECT status FROM transport.trips
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        string currentStatus;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy chuyến đi." });
            currentStatus = result.ToString()!;
        }

        if (currentStatus != "Ready")
            return Conflict(new { message = $"Chỉ có thể bắt đầu chuyến ở trạng thái Ready. Trạng thái hiện tại: {currentStatus}." });

        // Transition via state machine
        TripStateMachine.EnsureCanTransition(TripStatus.Ready, TripStatus.InProgress);

        const string updateSql = """
            UPDATE transport.trips SET
                status = 'InProgress',
                started_at = NOW(),
                updated_at = NOW()
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Audit
        await InsertAuditLog(conn, "Trip", tripId, "Started", driverId, ct);

        return Ok(new { message = "Đã bắt đầu chuyến đi." });
    }

    // ─── PUT /api/driver/trips/{tripId}/complete ───────────────────────
    [HttpPut("trips/{tripId:guid}/complete")]
    public async Task<IActionResult> CompleteTrip(Guid tripId, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify ownership and status
        const string checkSql = """
            SELECT status FROM transport.trips
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        string currentStatus;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy chuyến đi." });
            currentStatus = result.ToString()!;
        }

        if (currentStatus != "InProgress")
            return Conflict(new { message = $"Chỉ có thể hoàn thành chuyến ở trạng thái InProgress. Trạng thái hiện tại: {currentStatus}." });

        // Check all shipments are in valid terminal state
        const string checkShipments = """
            SELECT COUNT(*) FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id AND s.is_deleted = FALSE
            WHERE ts.trip_id = @trip_id AND ts.is_deleted = FALSE
            AND s.status NOT IN ('Delivered', 'Completed', 'Cancelled');
        """;
        await using (var cmd = new NpgsqlCommand(checkShipments, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            var activeCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
            if (activeCount > 0)
                return Conflict(new { message = $"Còn {activeCount} kiện hàng chưa hoàn tất. Tất cả kiện hàng phải ở trạng thái Đã giao hàng, Hoàn tất, hoặc Đã hủy." });
        }

        // Transition via state machine
        TripStateMachine.EnsureCanTransition(TripStatus.InProgress, TripStatus.Completed);

        const string updateSql = """
            UPDATE transport.trips SET
                status = 'Completed',
                completed_at = NOW(),
                finished_at = NOW(),
                updated_at = NOW()
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Audit
        await InsertAuditLog(conn, "Trip", tripId, "Completed", driverId, ct);

        return Ok(new { message = "Đã hoàn thành chuyến đi." });
    }

    // ─── PUT /api/driver/shipments/{shipmentId}/confirm-pickup ─────────
    [HttpPut("shipments/{shipmentId:guid}/confirm-pickup")]
    public async Task<IActionResult> ConfirmPickup(Guid shipmentId, [FromBody] ConfirmPickupRequest request, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify shipment belongs to a trip of this driver
        const string verifySql = """
            SELECT s.status, s.pickup_address, t.status AS trip_status
            FROM warehouse.shipments s
            JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
            JOIN transport.trips t ON t.id = ts.trip_id AND t.driver_id = @driver_id AND t.is_deleted = FALSE
            WHERE s.id = @shipment_id AND s.is_deleted = FALSE;
        """;
        string shipmentStatus, pickupAddress, tripStatus;
        await using (var cmd = new NpgsqlCommand(verifySql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return NotFound(new { message = "Không tìm thấy kiện hàng trong chuyến đi của bạn." });
            shipmentStatus = reader.GetString(0);
            pickupAddress = reader.IsDBNull(1) ? "" : reader.GetString(1);
            tripStatus = reader.GetString(2);
        }

        // Validate conditions
        if (shipmentStatus != "Matched")
            return Conflict(new { message = $"Chỉ có thể xác nhận nhận hàng cho kiện ở trạng thái Matched. Trạng thái hiện tại: {shipmentStatus}." });
        if (string.IsNullOrWhiteSpace(pickupAddress))
            return BadRequest(new { message = "Đơn hàng chưa có thông tin địa chỉ nhận hàng." });
        if (tripStatus != "Ready" && tripStatus != "InProgress")
            return Conflict(new { message = $"Chuyến đi phải ở trạng thái Ready hoặc InProgress. Trạng thái hiện tại: {tripStatus}." });

        // Transition Matched → In_Transit
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            const string updateSql = """
                UPDATE warehouse.shipments SET
                    status = 'In_Transit',
                    picked_up_at = NOW(),
                    picked_up_by = @driver_id,
                    updated_at = NOW()
                WHERE id = @shipment_id AND is_deleted = FALSE;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                cmd.Parameters.AddWithValue("driver_id", driverId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit history
            const string historySql = """
                INSERT INTO warehouse.shipment_status_history (shipment_id, from_status, to_status, performed_by, reason, occurred_at)
                VALUES (@shipment_id, 'Matched', 'In_Transit', @driver_id, @reason, NOW());
            """;
            await using (var cmd = new NpgsqlCommand(historySql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                cmd.Parameters.AddWithValue("driver_id", driverId);
                cmd.Parameters.AddWithValue("reason", (object?)request.PickupNote ?? "Xác nhận nhận hàng.");
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit log
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('Shipment', @entity_id, 'ConfirmPickup', @performed_by, @details, NOW());
            """;
            await using (var cmd = new NpgsqlCommand(auditSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("entity_id", shipmentId);
                cmd.Parameters.AddWithValue("performed_by", driverId);
                cmd.Parameters.AddWithValue("details", System.Text.Json.JsonSerializer.Serialize(new { pickupNote = request.PickupNote }));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return Ok(new { message = "Đã xác nhận nhận hàng.", status = "In_Transit" });
    }

    // ─── PUT /api/driver/shipments/{shipmentId}/start-transport ────────
    [HttpPut("shipments/{shipmentId:guid}/start-transport")]
    public async Task<IActionResult> StartTransport(Guid shipmentId, [FromBody] StartTransportRequest request, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify shipment in warehouse (Hub flow) belongs to driver's trip
        const string verifySql = """
            SELECT s.status, t.status AS trip_status
            FROM warehouse.shipments s
            JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
            JOIN transport.trips t ON t.id = ts.trip_id AND t.driver_id = @driver_id AND t.is_deleted = FALSE
            WHERE s.id = @shipment_id AND s.is_deleted = FALSE;
        """;
        string shipmentStatus, tripStatus;
        await using (var cmd = new NpgsqlCommand(verifySql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return NotFound(new { message = "Không tìm thấy kiện hàng trong chuyến đi của bạn." });
            shipmentStatus = reader.GetString(0);
            tripStatus = reader.GetString(1);
        }

        if (shipmentStatus != "In_Warehouse")
            return Conflict(new { message = $"Chỉ có thể bắt đầu vận chuyển kiện ở trạng thái In_Warehouse. Trạng thái hiện tại: {shipmentStatus}." });
        if (tripStatus != "InProgress")
            return Conflict(new { message = $"Chuyến đi phải ở trạng thái InProgress." });

        // Transition In_Warehouse → In_Transit
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            const string updateSql = """
                UPDATE warehouse.shipments SET
                    status = 'In_Transit',
                    updated_at = NOW()
                WHERE id = @shipment_id AND is_deleted = FALSE;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            const string historySql = """
                INSERT INTO warehouse.shipment_status_history (shipment_id, from_status, to_status, performed_by, reason, occurred_at)
                VALUES (@shipment_id, 'In_Warehouse', 'In_Transit', @driver_id, @reason, NOW());
            """;
            await using (var cmd = new NpgsqlCommand(historySql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                cmd.Parameters.AddWithValue("driver_id", driverId);
                cmd.Parameters.AddWithValue("reason", (object?)request.Note ?? "Bắt đầu vận chuyển từ kho.");
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return Ok(new { message = "Đã bắt đầu vận chuyển.", status = "In_Transit" });
    }

    // ─── PUT /api/driver/shipments/{shipmentId}/confirm-delivery ───────
    [HttpPut("shipments/{shipmentId:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(Guid shipmentId, [FromBody] ConfirmDeliveryRequest request, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify shipment belongs to driver's trip
        const string verifySql = """
            SELECT s.status, t.status AS trip_status
            FROM warehouse.shipments s
            JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
            JOIN transport.trips t ON t.id = ts.trip_id AND t.driver_id = @driver_id AND t.is_deleted = FALSE
            WHERE s.id = @shipment_id AND s.is_deleted = FALSE;
        """;
        string shipmentStatus, tripStatus;
        await using (var cmd = new NpgsqlCommand(verifySql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return NotFound(new { message = "Không tìm thấy kiện hàng trong chuyến đi của bạn." });
            shipmentStatus = reader.GetString(0);
            tripStatus = reader.GetString(1);
        }

        if (shipmentStatus != "In_Transit")
            return Conflict(new { message = $"Chỉ có thể xác nhận giao hàng cho kiện ở trạng thái In_Transit. Trạng thái hiện tại: {shipmentStatus}." });

        // Transition In_Transit → Delivered
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            const string updateSql = """
                UPDATE warehouse.shipments SET
                    status = 'Delivered',
                    delivered_at = NOW(),
                    delivered_by = @driver_id,
                    delivery_note = @delivery_note,
                    proof_image_url = @proof_image_url,
                    updated_at = NOW()
                WHERE id = @shipment_id AND is_deleted = FALSE;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                cmd.Parameters.AddWithValue("driver_id", driverId);
                cmd.Parameters.AddWithValue("delivery_note", (object?)request.DeliveryNote ?? DBNull.Value);
                cmd.Parameters.AddWithValue("proof_image_url", (object?)request.ProofImageUrl ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            const string historySql = """
                INSERT INTO warehouse.shipment_status_history (shipment_id, from_status, to_status, performed_by, reason, occurred_at)
                VALUES (@shipment_id, 'In_Transit', 'Delivered', @driver_id, @reason, NOW());
            """;
            await using (var cmd = new NpgsqlCommand(historySql, conn, tx))
            {
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                cmd.Parameters.AddWithValue("driver_id", driverId);
                cmd.Parameters.AddWithValue("reason", (object?)request.DeliveryNote ?? "Xác nhận giao hàng.");
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit log
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('Shipment', @entity_id, 'ConfirmDelivery', @performed_by, @details, NOW());
            """;
            await using (var cmd = new NpgsqlCommand(auditSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("entity_id", shipmentId);
                cmd.Parameters.AddWithValue("performed_by", driverId);
                cmd.Parameters.AddWithValue("details", System.Text.Json.JsonSerializer.Serialize(new { deliveryNote = request.DeliveryNote }));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return Ok(new { message = "Đã xác nhận giao hàng thành công.", status = "Delivered" });
    }

    // ─── POST /api/driver/trips/{tripId}/incidents ─────────────────────
    [HttpPost("trips/{tripId:guid}/incidents")]
    public async Task<IActionResult> ReportIncident(Guid tripId, [FromBody] ReportIncidentRequest request, CancellationToken ct)
    {
        var driverId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify trip belongs to driver
        const string verifySql = """
            SELECT id FROM transport.trips
            WHERE id = @trip_id AND driver_id = @driver_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(verifySql, conn))
        {
            cmd.Parameters.AddWithValue("trip_id", tripId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            if (await cmd.ExecuteScalarAsync(ct) == null)
                return Forbid();
        }

        // Insert incident
        const string insertSql = """
            INSERT INTO transport.trip_incidents (trip_id, shipment_id, reported_by, incident_type, description, occurred_at, created_at)
            VALUES (@trip_id, @shipment_id, @reported_by, @incident_type, @description, @occurred_at, NOW())
            RETURNING id;
        """;
        await using var cmd2 = new NpgsqlCommand(insertSql, conn);
        cmd2.Parameters.AddWithValue("trip_id", tripId);
        cmd2.Parameters.AddWithValue("shipment_id", (object?)request.ShipmentId ?? DBNull.Value);
        cmd2.Parameters.AddWithValue("reported_by", driverId);
        cmd2.Parameters.AddWithValue("incident_type", request.IncidentType);
        cmd2.Parameters.AddWithValue("description", request.Description);
        cmd2.Parameters.AddWithValue("occurred_at", request.OccurredAt);
        var incidentId = await cmd2.ExecuteScalarAsync(ct);

        // Audit
        await InsertAuditLog(conn, "Trip", tripId, "IncidentReported", driverId, ct);

        return StatusCode(201, new { id = incidentId, message = "Đã báo cáo sự cố." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static DriverAllowedActions ComputeTripAllowedActions(string status) => status switch
    {
        "Scheduled" => new() { CanView = true, CanStart = false, CanComplete = false },
        "Ready" => new() { CanView = true, CanStart = true, CanComplete = false },
        "InProgress" => new() { CanView = true, CanStart = false, CanComplete = true },
        "Completed" => new() { CanView = true, CanStart = false, CanComplete = false },
        "Cancelled" => new() { CanView = true, CanStart = false, CanComplete = false },
        "Breakdown" => new() { CanView = true, CanStart = false, CanComplete = false },
        "Active" => new() { CanView = true, CanStart = false, CanComplete = false },
        _ => new() { CanView = true }
    };

    private static DriverShipmentAllowedActions ComputeDriverShipmentAllowedActions(string status) => status switch
    {
        "Matched" => new() { CanView = true, CanConfirmPickup = true, CanStartTransport = false, CanConfirmDelivery = false },
        "In_Warehouse" => new() { CanView = true, CanConfirmPickup = false, CanStartTransport = true, CanConfirmDelivery = false },
        "In_Transit" => new() { CanView = true, CanConfirmPickup = false, CanStartTransport = false, CanConfirmDelivery = true },
        "Delivered" => new() { CanView = true, CanConfirmPickup = false, CanStartTransport = false, CanConfirmDelivery = false },
        "Completed" => new() { CanView = true, CanConfirmPickup = false, CanStartTransport = false, CanConfirmDelivery = false },
        "Cancelled" => new() { CanView = true, CanConfirmPickup = false, CanStartTransport = false, CanConfirmDelivery = false },
        _ => new() { CanView = true }
    };

    private static async Task<List<DriverShipmentListItem>> LoadTripShipments(NpgsqlConnection conn, Guid tripId, CancellationToken ct)
    {
        const string sql = """
            SELECT s.id, s.qr_code, s.cargo_type, s.weight_kg, s.volume_cbm, s.status,
                   s.sender_name, s.sender_phone, s.pickup_address,
                   s.receiver_name, s.receiver_phone, s.dest_address
            FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id AND s.is_deleted = FALSE
            WHERE ts.trip_id = @trip_id AND ts.is_deleted = FALSE
            ORDER BY ts.delivery_sequence ASC;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("trip_id", tripId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<DriverShipmentListItem>();
        var shipmentIds = new List<Guid>();
        var shipmentsByStatus = new Dictionary<Guid, string>();

        while (await reader.ReadAsync(ct))
        {
            var shipmentId = reader.GetGuid(0);
            var status = reader.GetString(5);
            shipmentIds.Add(shipmentId);
            shipmentsByStatus[shipmentId] = status;
            items.Add(new DriverShipmentListItem
            {
                Id = shipmentId,
                ShipmentCode = reader.GetString(1),
                Commodity = reader.IsDBNull(2) ? null : reader.GetString(2),
                Weight = reader.GetDecimal(3),
                Volume = reader.GetDecimal(4),
                Status = status,
                SenderName = reader.IsDBNull(6) ? null : reader.GetString(6),
                SenderPhone = reader.IsDBNull(7) ? null : reader.GetString(7),
                PickupAddress = reader.IsDBNull(8) ? null : reader.GetString(8),
                ReceiverName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ReceiverPhone = reader.IsDBNull(10) ? null : reader.GetString(10),
                DeliveryAddress = reader.IsDBNull(11) ? null : reader.GetString(11),
                AllowedActions = ComputeDriverShipmentAllowedActions(status)
            });
        }
        await reader.CloseAsync();

        // Batch-fetch pending FinalPayment (COD) IDs for Delivered shipments
        if (shipmentIds.Count > 0)
        {
            var deliveredIds = shipmentIds.Where(id => shipmentsByStatus.GetValueOrDefault(id) == "Delivered").ToList();
            if (deliveredIds.Count > 0)
            {
                var placeholders = string.Join(",", deliveredIds.Select((_, i) => $"@sid{i}"));
                var codSql = $"""
                    SELECT shipment_id, id, amount, currency, payment_code FROM warehouse.payments
                    WHERE shipment_id IN ({placeholders})
                      AND payment_type = 'FinalPayment' AND payment_method = 'COD' AND status = 'Pending'
                      AND is_deleted = FALSE;
                """;
                await using var codCmd = new NpgsqlCommand(codSql, conn);
                for (int i = 0; i < deliveredIds.Count; i++)
                    codCmd.Parameters.AddWithValue($"@sid{i}", deliveredIds[i]);

                await using var codReader = await codCmd.ExecuteReaderAsync(ct);
                var codMap = new Dictionary<Guid, (Guid PaymentId, decimal Amount, string Currency, string PaymentCode)>();
                while (await codReader.ReadAsync(ct))
                {
                    var shipId = codReader.GetGuid(0);
                    var paymentId = codReader.GetGuid(1);
                    var amount = codReader.GetDecimal(2);
                    var currency = codReader.GetString(3);
                    var paymentCode = codReader.IsDBNull(4) ? null : codReader.GetString(4);
                    codMap[shipId] = (paymentId, amount, currency, paymentCode);
                }
                await codReader.CloseAsync();

                // Update items with COD payment info
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (codMap.TryGetValue(item.Id, out var cod))
                    {
                        items[i] = item with
                        {
                            PendingCodPaymentId = cod.PaymentId,
                            PendingCodAmount = cod.Amount,
                            PendingCodCurrency = cod.Currency,
                            PendingCodPaymentCode = cod.PaymentCode,
                            AllowedActions = item.AllowedActions with { CanConfirmCod = true }
                        };
                    }
                }
            }
        }

        return items;
    }

    private static async Task<List<TripTimelineEntry>> LoadTripTimeline(NpgsqlConnection conn, Guid tripId, CancellationToken ct)
    {
        // Build timeline from trip status history (simplified)
        var timeline = new List<TripTimelineEntry>();
        const string sql = """
            SELECT status, started_at, completed_at, created_at FROM transport.trips WHERE id = @trip_id;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("trip_id", tripId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var status = reader.GetString(0);
            var createdAt = reader.GetDateTime(3);

            timeline.Add(new TripTimelineEntry { Label = "Đã lên lịch", Timestamp = createdAt, IsCompleted = true });

            if (status is "Ready" or "InProgress" or "Completed")
                timeline.Add(new TripTimelineEntry { Label = "Sẵn sàng", Timestamp = createdAt, IsCompleted = true });

            if (status is "InProgress" or "Completed")
                timeline.Add(new TripTimelineEntry { Label = "Đang thực hiện", Timestamp = reader.IsDBNull(1) ? null : reader.GetDateTime(1), IsCompleted = true });

            if (status == "Completed")
                timeline.Add(new TripTimelineEntry { Label = "Hoàn tất", Timestamp = reader.IsDBNull(2) ? null : reader.GetDateTime(2), IsCompleted = true });

            if (status == "Cancelled")
                timeline.Add(new TripTimelineEntry { Label = "Đã hủy", Timestamp = null, IsCompleted = true });
        }
        return timeline;
    }

    private static async Task<List<TimelineEntry>> LoadShipmentTimeline(NpgsqlConnection conn, Guid shipmentId, CancellationToken ct)
    {
        const string sql = """
            SELECT to_status, occurred_at
            FROM warehouse.shipment_status_history
            WHERE shipment_id = @shipment_id
            ORDER BY occurred_at ASC;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var timeline = new List<TimelineEntry>();
        while (await reader.ReadAsync(ct))
        {
            var toStatus = reader.GetString(0);
            var occurredAt = reader.GetDateTime(1);
            timeline.Add(new TimelineEntry
            {
                Label = StatusToVietnamese(toStatus),
                Timestamp = occurredAt,
                IsCompleted = true,
                IsCurrent = false
            });
        }
        return timeline;
    }

    private static async Task InsertAuditLog(NpgsqlConnection conn, string entityType, Guid entityId, string action, Guid performedBy, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, created_at)
            VALUES (@entity_type, @entity_id, @action, @performed_by, NOW());
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("entity_type", entityType);
        cmd.Parameters.AddWithValue("entity_id", entityId);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("performed_by", performedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string StatusToVietnamese(string status) => status switch
    {
        "Draft" => "Bản nháp",
        "PendingReview" => "Chờ duyệt",
        "PendingDeposit" => "Chờ đặt cọc",
        "In_Warehouse" => "Đã vào kho",
        "Matched" => "Đã ghép chuyến",
        "In_Transit" => "Đang vận chuyển",
        "Delivered" => "Đã giao hàng",
        "Completed" => "Hoàn tất",
        "Cancelled" => "Đã hủy",
        _ => status
    };
}
