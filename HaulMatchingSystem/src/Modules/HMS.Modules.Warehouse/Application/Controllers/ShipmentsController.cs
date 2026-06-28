using HMS.Modules.Warehouse.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.Extensions.Configuration;

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
}