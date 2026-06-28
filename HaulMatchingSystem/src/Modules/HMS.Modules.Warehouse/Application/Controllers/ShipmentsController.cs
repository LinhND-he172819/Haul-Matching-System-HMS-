using HMS.Modules.Warehouse.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ShipmentsController(IConfiguration configuration)
    {
        _configuration = configuration;
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

    [HttpGet("qr/{qrCode}")]
    public async Task<ActionResult<ShipmentQrLookupResponse>> GetByQrCode(string qrCode)
    {
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
        cmd.Parameters.AddWithValue("qr_code", qrCode);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound("Không tìm thấy đơn hàng theo mã QR.");

        return Ok(new ShipmentQrLookupResponse
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
        });
    }

    [Authorize]
    [HttpPut("{id:guid}/confirm-intake")]
    public async Task<IActionResult> ConfirmIntake(
    Guid id,
    [FromBody] ConfirmIntakeRequest request)
    {
        if (request.ActualWeightKg <= 0 || request.ActualVolumeCbm <= 0)
            return BadRequest("Cân nặng và thể tích thực tế phải lớn hơn 0.");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var staffId))
            return Unauthorized("Không xác định được nhân viên đăng nhập.");

        var hubClaim = User.FindFirst("HubId")?.Value;

        if (!Guid.TryParse(hubClaim, out var hubId))
            return BadRequest("Tài khoản nhân viên chưa được gán Hub.");

        await using var conn = new NpgsqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        const string sql = """
        UPDATE warehouse.shipments
        SET
            weight_kg = @actual_weight_kg,
            volume_cbm = @actual_volume_cbm,
            status = 'In_Warehouse',
            current_hub_id = @hub_id,
            intake_confirmed_by = @staff_id,
            intake_confirmed_at = NOW(),
            updated_at = NOW()
        WHERE id = @id
          AND status = 'Draft'
          AND is_deleted = FALSE
        RETURNING id;
    """;

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("actual_weight_kg", request.ActualWeightKg);
        cmd.Parameters.AddWithValue("actual_volume_cbm", request.ActualVolumeCbm);
        cmd.Parameters.AddWithValue("hub_id", hubId);
        cmd.Parameters.AddWithValue("staff_id", staffId);

        var updatedId = await cmd.ExecuteScalarAsync();

        if (updatedId is null)
            return BadRequest("Chỉ đơn ở trạng thái Draft mới được nhập kho.");

        return Ok(new
        {
            message = "Nhập kho thành công.",
            status = "In_Warehouse",
            currentHubId = hubId,
            intakeConfirmedBy = staffId,
            intakeConfirmedAt = DateTime.UtcNow
        });
    }
}