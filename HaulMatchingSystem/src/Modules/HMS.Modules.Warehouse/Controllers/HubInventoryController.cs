using System.Security.Claims;
using HMS.Modules.Warehouse.Application.DTOs;
using HMS.Modules.Warehouse.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/hub-inventory")]
[Authorize]
public class HubInventoryController : ControllerBase
{
    private readonly IHubInventoryService _service;
    private readonly string _connStr;

    public HubInventoryController(IHubInventoryService service, IConfiguration configuration)
    {
        _service = service;
        _connStr = configuration.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Lấy danh sách kiện hàng trong kho Hub.
    /// Admin: có thể truyền hubId.
    /// Warehouse_Staff: tự động lấy hubId từ JWT.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromQuery] string? cargoType = null,
        [FromQuery] Guid? hubId = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken ct = default)
    {
        var effectiveHubId = ResolveHubId(hubId);

        var query = new HubInventoryQuery
        {
            Page = page,
            PageSize = pageSize,
            Keyword = keyword,
            Status = status,
            CargoType = cargoType,
            HubId = effectiveHubId,
            Sort = sort,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _service.GetInventoryAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Dashboard summary KPI
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? hubId = null,
        CancellationToken ct = default)
    {
        var effectiveHubId = ResolveHubId(hubId);
        var result = await _service.GetDashboardSummaryAsync(effectiveHubId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Chi tiết kiện hàng
    /// </summary>
    [HttpGet("{shipmentId:guid}")]
    public async Task<IActionResult> GetDetail(Guid shipmentId, CancellationToken ct = default)
    {
        var result = await _service.GetDetailAsync(shipmentId, ct);
        if (result == null)
            return NotFound(new { message = "Không tìm thấy kiện hàng." });

        return Ok(result);
    }

    /// <summary>
    /// Cập nhật thông tin kiện hàng
    /// </summary>
    [HttpPut("{shipmentId:guid}")]
    public async Task<IActionResult> Update(
        Guid shipmentId,
        [FromBody] UpdateShipmentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _service.UpdateShipmentAsync(shipmentId, request, ct);
            return Ok(new { message = "Cập nhật thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Nếu là Warehouse_Staff thì trả về hub_id từ JWT,
    /// nếu là Admin thì trả về hubId truyền vào (hoặc null để xem tất cả).
    /// </summary>
    private Guid? ResolveHubId(Guid? requestedHubId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role == "Warehouse_Staff")
        {
            var hubIdClaim = User.FindFirst("hub_id")?.Value;
            if (Guid.TryParse(hubIdClaim, out var staffHubId))
                return staffHubId;

            // Fallback: read from DB using userId
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                // Hub staff must have a hubId
                return GetHubIdFromUser(userId);
            }

            return null; // Shouldn't happen for valid staff
        }

        // Admin or other roles: use requested hubId (null means all hubs)
        return requestedHubId;
    }

    private Guid? GetHubIdFromUser(Guid userId)
    {
        if (string.IsNullOrEmpty(_connStr))
            return null;

        using var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        using var cmd = new NpgsqlCommand(
            "SELECT hub_id FROM identity.users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        var result = cmd.ExecuteScalar();
        if (result is Guid hubId)
            return hubId;
        return null;
    }
}
