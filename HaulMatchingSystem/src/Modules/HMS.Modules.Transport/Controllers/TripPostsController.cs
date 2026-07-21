using HMS.Modules.Transport.Application.DTOs.TripPosts;
using HMS.Modules.Transport.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace HMS.Modules.Transport.Controllers;

[ApiController]
[Route("api/trip-posts")]
public class TripPostsController : ControllerBase
{
    private readonly ITripPostService _service;
    private readonly ILogger<TripPostsController> _logger;

    public TripPostsController(ITripPostService service, ILogger<TripPostsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role)!;

    private Guid? GetJwtHubId()
    {
        var hubClaim = User.FindFirst("HubId")?.Value;
        return Guid.TryParse(hubClaim, out var hubId) ? hubId : null;
    }

    private IActionResult HandleException(Exception ex)
    {
        return ex switch
        {
            KeyNotFoundException => NotFound(new { message = ex.Message }),
            UnauthorizedAccessException => Forbid(),
            ArgumentException => BadRequest(new { message = ex.Message }),
            InvalidOperationException ex2 when ex2.Message.Contains("đã có") =>
                Conflict(new { message = ex2.Message }),
            InvalidOperationException ex3 => Conflict(new { message = ex3.Message }),
            _ => LogAndReturn500(ex)
        };
    }

    private IActionResult LogAndReturn500(Exception ex)
    {
        _logger.LogError(ex, "Unhandled error in TripPostsController");
        return StatusCode(500, new { message = "Đã xảy ra lỗi server. Vui lòng thử lại." });
    }

    // ── 5.1 Eligible trips ──────────────────────────────────────────

    [HttpGet("eligible-trips")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> GetEligibleTrips(
        [FromQuery] Guid? hubId,
        [FromQuery] string? keyword,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetEligibleTripsAsync(
                GetUserId(), GetRole(), hubId, keyword, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.2 Create ──────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripPostRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(
                GetUserId(), GetRole(), GetJwtHubId(), request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.3 List (admin/staff) ──────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? hubId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken ct = default)
    {
        try
        {
            var filter = new TripPostFilterRequest
            {
                Page = page,
                PageSize = pageSize,
                Keyword = keyword,
                Status = status,
                HubId = hubId,
                FromDate = fromDate,
                ToDate = toDate,
                SortBy = sortBy,
                SortDirection = sortDirection
            };

            var result = await _service.ListAsync(
                GetUserId(), GetRole(), GetJwtHubId(), filter, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.4 Detail ──────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetByIdAsync(
                GetUserId(), GetRole(), GetJwtHubId(), id, ct);
            return result == null ? NotFound(new { message = "Không tìm thấy bài đăng." }) : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.5 Update ──────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTripPostRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.UpdateAsync(
                GetUserId(), GetRole(), GetJwtHubId(), id, request, ct);
            return result == null ? NotFound(new { message = "Không tìm thấy bài đăng." }) : Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.6 Close ───────────────────────────────────────────────────

    [HttpPut("{id:guid}/close")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        try
        {
            var success = await _service.CloseAsync(
                GetUserId(), GetRole(), GetJwtHubId(), id, ct);
            return success
                ? Ok(new { message = "Đã đóng bài đăng." })
                : NotFound(new { message = "Không tìm thấy bài đăng." });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.7 Cancel ──────────────────────────────────────────────────

    [HttpPut("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelTripPostRequest? request,
        CancellationToken ct)
    {
        try
        {
            var success = await _service.CancelAsync(
                GetUserId(), GetRole(), GetJwtHubId(), id, request, ct);
            return success
                ? Ok(new { message = "Đã hủy bài đăng." })
                : NotFound(new { message = "Không tìm thấy bài đăng." });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── 5.8 Public list ─────────────────────────────────────────────

    [HttpGet("public")]
    public async Task<IActionResult> ListPublic(
        [FromQuery] Guid? originHubId,
        [FromQuery] Guid? destinationHubId,
        [FromQuery] string? keyword,
        [FromQuery] DateTimeOffset? departureFrom,
        [FromQuery] DateTimeOffset? departureTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var filter = new PublicTripPostFilterRequest
            {
                Page = page,
                PageSize = pageSize,
                OriginHubId = originHubId,
                DestinationHubId = destinationHubId,
                Keyword = keyword,
                DepartureFrom = departureFrom,
                DepartureTo = departureTo
            };

            var result = await _service.ListPublicAsync(filter, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ── KPI ─────────────────────────────────────────────────────────

    [HttpGet("kpi")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public async Task<IActionResult> GetKpi(CancellationToken ct)
    {
        try
        {
            var result = await _service.GetKpiAsync(
                GetUserId(), GetRole(), GetJwtHubId(), ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
