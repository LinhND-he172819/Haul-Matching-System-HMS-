using HMS.Shared.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Warehouse.Application.Controllers;

/// <summary>
/// API for viewing shipment status change history (read-only audit trail).
/// </summary>
[ApiController]
[Route("api/shipments")]
[Authorize(Roles = "Admin,Warehouse_Staff")]
public class ShipmentStatusHistoryController : ControllerBase
{
    private readonly string _connStr;

    public ShipmentStatusHistoryController(IConfiguration configuration)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=hms_db;Username=postgres;Password=hms_password_123";
    }

    /// <summary>
    /// GET /api/shipments/{id}/status-history
    /// Returns the full status change history for a shipment.
    /// </summary>
    [HttpGet("{id:guid}/status-history")]
    public async Task<IActionResult> GetStatusHistory(Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                h.id,
                h.shipment_id,
                h.from_status,
                h.to_status,
                h.performed_by,
                u.full_name AS performed_by_name,
                h.reason,
                h.occurred_at
            FROM warehouse.shipment_status_history h
            LEFT JOIN identity.users u ON u.id = h.performed_by
            WHERE h.shipment_id = @shipment_id
            ORDER BY h.occurred_at ASC;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", id);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                id = reader.GetGuid(0),
                shipmentId = reader.GetGuid(1),
                fromStatus = reader.GetString(2),
                toStatus = reader.GetString(3),
                performedBy = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4),
                performedByName = reader.IsDBNull(5) ? null : reader.GetString(5),
                reason = reader.IsDBNull(6) ? null : reader.GetString(6),
                occurredAt = reader.GetDateTime(7)
            });
        }

        return Ok(new { shipmentId = id, history = items });
    }
}
