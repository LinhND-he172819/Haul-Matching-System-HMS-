using System.Security.Claims;
using HMS.Modules.Warehouse.Application.DTOs;
using HMS.Modules.Warehouse.Application.Services;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IShipmentStateService _shipmentStateService;

    public ShipmentsController(IConfiguration configuration, IShipmentStateService shipmentStateService)
    {
        _configuration = configuration;
        _shipmentStateService = shipmentStateService;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/shipments/qr/{qrCode}
    // Tìm shipment theo mã QR (dùng khi staff quét / nhập QR).
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    [HttpGet("qr/{qrCode}")]
    public async Task<ActionResult<ShipmentQrLookupResponse>> GetByQrCode(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return BadRequest(new { message = "Mã QR không được để trống." });

        var trimmed = qrCode.Trim();

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        const string sql = """
            SELECT
                id,
                qr_code,
                cargo_type,
                weight_kg,
                volume_cbm,
                receiver_name,
                receiver_phone,
                dest_address,
                status,
                special_handling_note
            FROM warehouse.shipments
            WHERE qr_code = @qr_code
              AND is_deleted = FALSE
            LIMIT 1;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("qr_code", trimmed);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return NotFound(new { message = "Không tìm thấy đơn hàng theo mã QR." });
        }

        var response = new ShipmentQrLookupResponse
        {
            Id = reader.GetGuid(0),
            QrCode = reader.GetString(1),
            CargoType = reader.GetString(2),
            WeightKg = reader.GetDecimal(3),
            VolumeCbm = reader.GetDecimal(4),
            ReceiverName = reader.GetString(5),
            ReceiverPhone = reader.GetString(6),
            DestAddress = reader.GetString(7),
            Status = reader.GetString(8),
            SpecialHandlingNote = reader.IsDBNull(9) ? null : reader.GetString(9)
        };

        return Ok(response);
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/shipments/{id}/confirm-intake
    // Xác nhận nhập kho – StaffId & HubId lấy từ JWT.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    [HttpPut("{id:guid}/confirm-intake")]
    public async Task<IActionResult> ConfirmIntake(
        Guid id,
        [FromBody] ConfirmIntakeRequest request)
    {
        // Validate actual measurements
        if (request.ActualWeightKg <= 0 || request.ActualVolumeCbm <= 0)
        {
            return BadRequest(new
            {
                message = "Cân nặng và thể tích thực tế phải lớn hơn 0."
            });
        }

        // ── Extract StaffId from JWT ──
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var staffId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được nhân viên đăng nhập."
            });
        }

        // ── Extract HubId from JWT ──
        var hubClaim = User.FindFirst("HubId")?.Value;

        if (!Guid.TryParse(hubClaim, out var hubId))
        {
            return BadRequest(new
            {
                message = "Tài khoản nhân viên chưa được gán Hub."
            });
        }

        // ── Open connection to first check & then update ──
        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── First: update weight/volume for intake ──
        const string updateSql = """
            UPDATE warehouse.shipments
            SET
                weight_kg = @actual_weight_kg,
                volume_cbm = @actual_volume_cbm,
                current_hub_id = @hub_id,
                intake_confirmed_by = @staff_id,
                intake_confirmed_at = NOW(),
                updated_at = NOW()
            WHERE id = @id
              AND status = 'Draft'
              AND is_deleted = FALSE
            RETURNING id;
        """;

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("actual_weight_kg", request.ActualWeightKg);
            cmd.Parameters.AddWithValue("actual_volume_cbm", request.ActualVolumeCbm);
            cmd.Parameters.AddWithValue("hub_id", hubId);
            cmd.Parameters.AddWithValue("staff_id", staffId);

            var updatedId = await cmd.ExecuteScalarAsync();

            if (updatedId == null)
            {
                // Check specific failure reason
                const string checkSql = """
                    SELECT status, is_deleted
                    FROM warehouse.shipments
                    WHERE id = @id;
                """;
                await using var checkCmd = new NpgsqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("id", id);

                await using var checkReader = await checkCmd.ExecuteReaderAsync();

                if (!await checkReader.ReadAsync())
                {
                    return NotFound(new { message = "Không tìm thấy đơn hàng." });
                }

                var isDeleted = checkReader.GetBoolean(1);
                if (isDeleted)
                {
                    return NotFound(new { message = "Đơn hàng đã bị xóa." });
                }

                var currentStatus = checkReader.GetString(0);
                return Conflict(new
                {
                    message = $"Chỉ đơn ở trạng thái Draft mới được nhập kho. Trạng thái hiện tại: {currentStatus}."
                });
            }
        }

        // ── Transition: Draft → In_Warehouse via State Machine ──
        try
        {
            await _shipmentStateService.TransitionAsync(
                id,
                ShipmentStatus.In_Warehouse,
                connection: conn,
                transaction: null,
                performedBy: staffId,
                ct: HttpContext.RequestAborted);
        }
        catch (HMS.Shared.Core.Exceptions.InvalidShipmentTransitionException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return Ok(new
        {
            message = "Nhập kho thành công.",
            status = "In_Warehouse",
            currentHubId = hubId.ToString(),
            intakeConfirmedBy = staffId.ToString(),
            intakeConfirmedAt = DateTimeOffset.UtcNow.ToString("o")
        });
    }

    [HttpPost("draft")]
    public async Task<ActionResult<DraftShipmentResponse>> CreateDraft(
        [FromBody] CreateDraftShipmentRequest request)
    {
        if (request.WeightKg <= 0 || request.VolumeCbm <= 0)
            return BadRequest("Weight and volume must be greater than 0.");

        var qrCode = $"GC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        const string sql = """
            INSERT INTO warehouse.shipments (
                qr_code,
                customer_id,
                sender_name,
                sender_phone,
                pickup_address,
                pickup_latitude,
                pickup_longitude,
                pickup_note,
                cargo_type,
                weight_kg,
                volume_cbm,
                receiver_name,
                receiver_phone,
                dest_address,
                dest_location,
                special_handling_note,
                status
            )
            VALUES (
                @qr_code,
                @customer_id,
                @sender_name,
                @sender_phone,
                @pickup_address,
                @pickup_latitude,
                @pickup_longitude,
                @pickup_note,
                @cargo_type,
                @weight_kg,
                @volume_cbm,
                @receiver_name,
                @receiver_phone,
                @dest_address,
                ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography,
                @special_handling_note,
                'Draft'
            )
            RETURNING id, qr_code, status, created_at;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("qr_code", qrCode);
        cmd.Parameters.AddWithValue("customer_id", request.CustomerId);
        cmd.Parameters.AddWithValue("sender_name", (object?)request.SenderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sender_phone", (object?)request.SenderPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pickup_address", (object?)request.PickupAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pickup_latitude", (object?)request.PickupLatitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pickup_longitude", (object?)request.PickupLongitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pickup_note", (object?)request.PickupNote ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cargo_type", request.CargoType);
        cmd.Parameters.AddWithValue("weight_kg", request.WeightKg);
        cmd.Parameters.AddWithValue("volume_cbm", request.VolumeCbm);
        cmd.Parameters.AddWithValue("receiver_name", request.ReceiverName);
        cmd.Parameters.AddWithValue("receiver_phone", request.ReceiverPhone);
        cmd.Parameters.AddWithValue("dest_address", request.DestAddress);
        cmd.Parameters.AddWithValue("lat", request.DestLat);
        cmd.Parameters.AddWithValue("lng", request.DestLng);
        cmd.Parameters.AddWithValue(
            "special_handling_note",
            (object?)request.SpecialHandlingNote ?? DBNull.Value
        );

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return StatusCode(500, "Cannot create draft shipment.");

        return Ok(new DraftShipmentResponse
        {
            Id = reader.GetGuid(0),
            QrCode = reader.GetString(1),
            Status = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3)
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/shipments/{shipmentId}/proposals
    // Customer xem danh sách đề xuất ghép chuyến của một shipment.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Customer")]
    [HttpGet("{shipmentId:guid}/proposals")]
    public async Task<IActionResult> GetProposals(Guid shipmentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var customerId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
        await conn.OpenAsync();

        const string sql = """
            SELECT
                ts.id,
                ts.trip_id,
                ts.shipment_id,
                ts.delivery_sequence,
                ts.status,
                ts.message,
                ts.suggested_at,
                ts.accepted_at,
                ts.rejected_at,
                ts.reject_reason,
                ts.cancelled_at,
                ts.cancelled_by
            FROM transport.trip_shipments ts
            WHERE ts.shipment_id = @shipment_id
            ORDER BY ts.suggested_at DESC;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);

        var proposals = new List<object>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            proposals.Add(new
            {
                id = reader.GetGuid(0),
                tripId = reader.GetGuid(1),
                shipmentId = reader.GetGuid(2),
                deliverySequence = reader.GetInt32(3),
                status = reader.GetString(4),
                message = reader.IsDBNull(5) ? null : reader.GetString(5),
                suggestedAt = reader.GetDateTime(6),
                acceptedAt = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                rejectedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                rejectedReason = reader.IsDBNull(9) ? null : reader.GetString(9),
                cancelledAt = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10),
                cancelledBy = reader.IsDBNull(11) ? (Guid?)null : reader.GetGuid(11),
            });
        }

        return Ok(proposals);
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/shipments/{shipmentId}/proposals
    // Customer tạo đề xuất ghép chuyến – gắn shipment vào trip_post.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Customer")]
    [HttpPost("{shipmentId:guid}/proposals")]
    public async Task<IActionResult> CreateProposal(
        Guid shipmentId,
        [FromBody] CreateShipmentProposalRequest request)
    {
        // ── Extract CustomerId from JWT ──
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var customerId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng." });
        }

        // ── Validate trip post ──
        if (request.TripPostId == Guid.Empty)
            return BadRequest(new { message = "Thiếu tripPostId." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── Check shipment exists & belongs to customer ──
        const string checkShipmentSql = """
            SELECT id, status FROM warehouse.shipments
            WHERE id = @shipment_id AND customer_id = @customer_id AND is_deleted = FALSE;
        """;

        await using (var cmd = new NpgsqlCommand(checkShipmentSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập." });
            }

            var status = reader.GetString(1);
            if (status != "Draft")
            {
                return Conflict(new { message = $"Chỉ đơn ở trạng thái Draft mới được đề xuất ghép chuyến. Trạng thái hiện tại: {status}." });
            }
        }

        // ── Validate trip post is Open & not expired ──
        const string checkTripPostSql = """
            SELECT tp.id, tp.trip_id, tp.accept_until, tp.status,
                   COALESCE(tp.pickup_mode, 'Hub') AS pickup_mode
            FROM transport.trip_posts tp
            WHERE tp.id = @trip_post_id
              AND tp.is_deleted = FALSE;
        """;

        Guid tripId;
        string tripPickupMode;

        await using (var cmd = new NpgsqlCommand(checkTripPostSql, conn))
        {
            cmd.Parameters.AddWithValue("trip_post_id", request.TripPostId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = "Không tìm thấy chuyến xe." });
            }

            var tpStatus = reader.GetString(3);

            if (tpStatus != "Open")
            {
                return Conflict(new { message = $"Chuyến xe không ở trạng thái nhận hàng. Trạng thái hiện tại: {tpStatus}." });
            }

            var acceptUntil = reader.GetDateTime(2);

            if (acceptUntil <= DateTimeOffset.UtcNow)
            {
                return Conflict(new { message = "Chuyến xe đã hết hạn nhận hàng." });
            }

            tripId = reader.GetGuid(1);
            tripPickupMode = reader.GetString(4);
        }

        // ── DirectPickup: validate sender fields on shipment ──
        if (tripPickupMode == "DirectPickup")
        {
            const string checkSenderSql = """
                SELECT sender_name, sender_phone, pickup_address
                FROM warehouse.shipments
                WHERE id = @shipment_id AND is_deleted = FALSE;
            """;

            await using (var senderCmd = new NpgsqlCommand(checkSenderSql, conn))
            {
                senderCmd.Parameters.AddWithValue("shipment_id", shipmentId);

                await using var senderReader = await senderCmd.ExecuteReaderAsync();

                if (await senderReader.ReadAsync())
                {
                    var senderName = senderReader.IsDBNull(0) ? null : senderReader.GetString(0);
                    var senderPhone = senderReader.IsDBNull(1) ? null : senderReader.GetString(1);
                    var pickupAddress = senderReader.IsDBNull(2) ? null : senderReader.GetString(2);

                    if (string.IsNullOrWhiteSpace(senderName) ||
                        string.IsNullOrWhiteSpace(senderPhone) ||
                        string.IsNullOrWhiteSpace(pickupAddress))
                    {
                        return BadRequest(new
                        {
                            message = "Chuyến xe nhận hàng tận nơi (DirectPickup). Vui lòng cập nhật thông tin người gửi (tên, số điện thoại, địa chỉ nhận) trước khi đề xuất ghép chuyến."
                        });
                    }
                }
            }
        }

        // ── Check for duplicate proposal ──
        const string checkDuplicateSql = """
            SELECT id FROM transport.trip_shipments
            WHERE shipment_id = @shipment_id AND trip_id = @trip_id
              AND status IN ('Suggested', 'Matched', 'In_Transit')
            LIMIT 1;
        """;

        await using (var dupCmd = new NpgsqlCommand(checkDuplicateSql, conn))
        {
            dupCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            dupCmd.Parameters.AddWithValue("trip_id", tripId);

            var existingId = await dupCmd.ExecuteScalarAsync();

            if (existingId != null)
            {
                return Conflict(new { message = "Đơn hàng này đã được đề xuất ghép chuyến cho chuyến xe này." });
            }
        }

        // ── Get next delivery_sequence ──
        const string seqSql = """
            SELECT COALESCE(MAX(delivery_sequence), 0) + 1
            FROM transport.trip_shipments
            WHERE trip_id = @trip_id;
        """;

        int nextSequence;

        await using (var seqCmd = new NpgsqlCommand(seqSql, conn))
        {
            seqCmd.Parameters.AddWithValue("trip_id", tripId);
            nextSequence = Convert.ToInt32(await seqCmd.ExecuteScalarAsync());
        }

        // ── Insert proposal ──
        const string insertSql = """
            INSERT INTO transport.trip_shipments (
                trip_id,
                shipment_id,
                delivery_sequence,
                status,
                message,
                suggested_at
            )
            VALUES (
                @trip_id,
                @shipment_id,
                @delivery_sequence,
                'Suggested',
                @message,
                NOW()
            )
            RETURNING id, trip_id, shipment_id, delivery_sequence, status, suggested_at;
        """;

        await using var insertCmd = new NpgsqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("trip_id", tripId);
        insertCmd.Parameters.AddWithValue("shipment_id", shipmentId);
        insertCmd.Parameters.AddWithValue("delivery_sequence", nextSequence);
        insertCmd.Parameters.AddWithValue("message", (object?)request.Message ?? DBNull.Value);

        await using var insertReader = await insertCmd.ExecuteReaderAsync();

        if (!await insertReader.ReadAsync())
        {
            return StatusCode(500, new { message = "Không thể tạo đề xuất ghép chuyến." });
        }

        return StatusCode(201, new CreateShipmentProposalResponse
        {
            Id = insertReader.GetGuid(0),
            ShipmentId = insertReader.GetGuid(2),
            TripPostId = request.TripPostId,
            Status = insertReader.GetString(4),
            Message = request.Message
        });
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/shipments/{shipmentId}/confirm-pickup
    // Driver xác nhận đã nhận hàng từ Customer (DirectPickup).
    // Chuyển trạng thái Draft → Matched.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Driver")]
    [HttpPut("{shipmentId:guid}/confirm-pickup")]
    public async Task<ActionResult<ConfirmPickupResponse>> ConfirmPickup(
        Guid shipmentId,
        [FromBody] ConfirmPickupRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var driverId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── Read shipment + validate ──
        const string readSql = """
            SELECT s.status, s.pickup_address, s.sender_name, s.sender_phone
            FROM warehouse.shipments s
            WHERE s.id = @shipment_id AND s.is_deleted = FALSE;
        """;

        string currentStatus;

        await using (var cmd = new NpgsqlCommand(readSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new { message = "Không tìm thấy đơn hàng." });

            currentStatus = reader.GetString(0);

            if (currentStatus != "Draft")
                return Conflict(new
                {
                    message = $"Chỉ đơn hàng ở trạng thái Draft mới có thể xác nhận nhận hàng. Trạng thái hiện tại: {currentStatus}."
                });

            var pickupAddress = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(pickupAddress))
                return BadRequest(new
                {
                    message = "Đơn hàng chưa có thông tin địa chỉ nhận hàng. Vui lòng cập nhật trước."
                });
        }

        // ── Transition Draft → Matched + write picked_up_at / picked_up_by ──
        const string updateSql = """
            UPDATE warehouse.shipments
            SET status = 'Matched',
                picked_up_at = NOW(),
                picked_up_by = @driver_id,
                updated_at = NOW()
            WHERE id = @shipment_id AND is_deleted = FALSE
            RETURNING id, status;
        """;

        Guid updatedId;

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("driver_id", driverId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return StatusCode(500, new { message = "Không thể cập nhật trạng thái đơn hàng." });

            updatedId = reader.GetGuid(0);
        }

        // ── Audit log ──
        const string auditSql = """
            INSERT INTO warehouse.shipment_status_history (
                shipment_id, from_status, to_status, performed_by, reason, occurred_at
            ) VALUES (
                @shipment_id, @from_status, 'Matched', @driver_id, @reason, NOW()
            );
        """;

        await using (var auditCmd = new NpgsqlCommand(auditSql, conn))
        {
            auditCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            auditCmd.Parameters.AddWithValue("from_status", currentStatus);
            auditCmd.Parameters.AddWithValue("driver_id", driverId);
            auditCmd.Parameters.AddWithValue("reason",
                (object?)request.PickupNote ?? "Driver xác nhận nhận hàng tại điểm giao.");

            await auditCmd.ExecuteNonQueryAsync();
        }

        return Ok(new ConfirmPickupResponse
        {
            ShipmentId = updatedId,
            Status = "Matched",
            PickedUpBy = driverId.ToString(),
            PickedUpAt = DateTimeOffset.UtcNow
        });
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/shipments/proposals/{proposalId}/accept
    // Driver chấp nhận đề xuất ghép chuyến.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Driver")]
    [HttpPut("proposals/{proposalId:guid}/accept")]
    public async Task<IActionResult> AcceptProposal(Guid proposalId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var driverId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── Read proposal ──
        const string readSql = """
            SELECT ts.id, ts.status, ts.trip_id, ts.shipment_id
            FROM transport.trip_shipments ts
            WHERE ts.id = @proposal_id AND ts.is_deleted = FALSE;
        """;

        Guid tripId;
        Guid shipmentId;
        string currentStatus;

        await using (var cmd = new NpgsqlCommand(readSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new { message = "Không tìm thấy đề xuất ghép chuyến." });

            currentStatus = reader.GetString(1);
            tripId = reader.GetGuid(2);
            shipmentId = reader.GetGuid(3);

            if (currentStatus != "Pending" && currentStatus != "Suggested")
            {
                return Conflict(new
                {
                    message = $"Chỉ đề xuất ở trạng thái Pending/Suggested mới được chấp nhận. Trạng thái hiện tại: {currentStatus}."
                });
            }
        }

        // ── Update proposal → Accepted ──
        const string updateProposalSql = """
            UPDATE transport.trip_shipments
            SET status = 'Accepted',
                accepted_at = NOW(),
                accepted_by = @driver_id
            WHERE id = @proposal_id;
        """;

        await using (var cmd = new NpgsqlCommand(updateProposalSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Update shipment status → Matched (via state machine validation) ──
        const string updateShipmentSql = """
            UPDATE warehouse.shipments
            SET status = 'Matched',
                updated_at = NOW()
            WHERE id = @shipment_id AND is_deleted = FALSE;
        """;

        await using (var cmd = new NpgsqlCommand(updateShipmentSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Audit log ──
        const string auditSql = """
            INSERT INTO warehouse.shipment_status_history (
                shipment_id, from_status, to_status, performed_by, reason, occurred_at
            ) VALUES (
                @shipment_id, 'Draft', 'Matched', @driver_id, 'Driver chấp nhận đề xuất ghép chuyến.', NOW()
            );
        """;

        await using (var auditCmd = new NpgsqlCommand(auditSql, conn))
        {
            auditCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            auditCmd.Parameters.AddWithValue("driver_id", driverId);
            await auditCmd.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Đã chấp nhận đề xuất ghép chuyến.", proposalId, status = "Accepted" });
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/shipments/proposals/{proposalId}/reject
    // Driver từ chối đề xuất ghép chuyến.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Driver")]
    [HttpPut("proposals/{proposalId:guid}/reject")]
    public async Task<IActionResult> RejectProposal(
        Guid proposalId,
        [FromBody] RespondToProposalRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var driverId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        if (string.IsNullOrWhiteSpace(request.RejectReason))
            return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── Read proposal ──
        const string readSql = """
            SELECT ts.id, ts.status, ts.trip_id, ts.shipment_id
            FROM transport.trip_shipments ts
            WHERE ts.id = @proposal_id AND ts.is_deleted = FALSE;
        """;

        string currentStatus;

        await using (var cmd = new NpgsqlCommand(readSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new { message = "Không tìm thấy đề xuất ghép chuyến." });

            currentStatus = reader.GetString(1);

            if (currentStatus != "Pending" && currentStatus != "Suggested")
            {
                return Conflict(new
                {
                    message = $"Chỉ đề xuất ở trạng thái Pending/Suggested mới được từ chối. Trạng thái hiện tại: {currentStatus}."
                });
            }
        }

        // ── Update proposal → Rejected ──
        const string updateSql = """
            UPDATE transport.trip_shipments
            SET status = 'Rejected',
                rejected_at = NOW(),
                rejected_by = @driver_id,
                reject_reason = @reject_reason
            WHERE id = @proposal_id;
        """;

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            cmd.Parameters.AddWithValue("driver_id", driverId);
            cmd.Parameters.AddWithValue("reject_reason", request.RejectReason);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Đã từ chối đề xuất ghép chuyến.", proposalId, status = "Rejected" });
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/shipments/proposals/{proposalId}/cancel
    // Customer hủy đề xuất ghép chuyến.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Customer")]
    [HttpPut("proposals/{proposalId:guid}/cancel")]
    public async Task<IActionResult> CancelProposal(Guid proposalId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var customerId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        // ── Read proposal + validate ownership ──
        const string readSql = """
            SELECT ts.id, ts.status, ts.shipment_id, s.customer_id
            FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id AND s.is_deleted = FALSE
            WHERE ts.id = @proposal_id AND ts.is_deleted = FALSE;
        """;

        string currentStatus;
        Guid shipmentId;

        await using (var cmd = new NpgsqlCommand(readSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new { message = "Không tìm thấy đề xuất ghép chuyến." });

            currentStatus = reader.GetString(1);
            shipmentId = reader.GetGuid(2);
            var ownerCustomerId = reader.GetGuid(3);

            if (ownerCustomerId != customerId)
                return Forbid();

            if (currentStatus != "Pending" && currentStatus != "Suggested")
            {
                return Conflict(new
                {
                    message = $"Chỉ đề xuất ở trạng thái Pending/Suggested mới được hủy. Trạng thái hiện tại: {currentStatus}."
                });
            }
        }

        // ── Update proposal → Cancelled ──
        const string updateSql = """
            UPDATE transport.trip_shipments
            SET status = 'Cancelled',
                cancelled_at = NOW(),
                cancelled_by = @customer_id
            WHERE id = @proposal_id;
        """;

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("proposal_id", proposalId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Đã hủy đề xuất ghép chuyến.", proposalId, status = "Cancelled" });
    }
}