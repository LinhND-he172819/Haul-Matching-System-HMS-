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
}