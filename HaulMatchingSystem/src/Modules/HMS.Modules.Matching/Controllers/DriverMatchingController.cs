using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Requests;
using Microsoft.Extensions.Logging;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Modules.Matching.Controllers
{
    [ApiController]
    [Route("api/drivers/me/matching-suggestions")]
    [Authorize]
    public class DriverMatchingController : ControllerBase
    {
        private readonly IMatchingService _service;
        private readonly ILogger<DriverMatchingController> _logger;

        public DriverMatchingController(IMatchingService service, ILogger<DriverMatchingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Get current matching suggestions for the authenticated driver.
        /// </summary>
        /// <returns>Matching suggestions payload including trip capacity and suggested shipments.</returns>
        [HttpGet]
        public async Task<IActionResult> GetSuggestions(CancellationToken ct)
        {
            // assume DriverId in claims sub
            var driverIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(driverIdClaim, out var driverId)) return Forbid();

            var res = await _service.GetSuggestionsForDriverAsync(driverId, ct);
            if (res == null) return NotFound();
            return Ok(res);
        }

        /// <summary>
        /// Accept all suggested shipments for the active trip.
        /// </summary>
        [HttpPost("accept-all")]
        public async Task<IActionResult> AcceptAll(CancellationToken ct)
        {
            var driverIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(driverIdClaim, out var driverId)) return Forbid();

            await _service.AcceptAllAsync(driverId, ct);
            return Ok(new { message = "Accepted" });
        }

        /// <summary>
        /// Reject all suggested shipments for the active trip.
        /// </summary>
        [HttpPost("reject-all")]
        public async Task<IActionResult> RejectAll(CancellationToken ct)
        {
            var driverIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(driverIdClaim, out var driverId)) return Forbid();

            await _service.RejectAllAsync(driverId, ct);
            return Ok(new { message = "Rejected" });
        }

        /// <summary>
        /// Accept selected shipments.
        /// </summary>
        [HttpPost("accept-selected")]
        public async Task<IActionResult> AcceptSelected([FromBody] AcceptSelectedRequest request, CancellationToken ct)
        {
            var driverIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(driverIdClaim, out var driverId)) return Forbid();

            await _service.AcceptSelectedAsync(driverId, request, ct);
            return Ok(new { message = "Accepted" });
        }

        /// <summary>
        /// Reject selected shipments.
        /// </summary>
        [HttpPost("reject-selected")]
        public async Task<IActionResult> RejectSelected([FromBody] RejectSelectedRequest request, CancellationToken ct)
        {
            var driverIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(driverIdClaim, out var driverId)) return Forbid();

            await _service.RejectSelectedAsync(driverId, request, ct);
            return Ok(new { message = "Rejected" });
        }
    }
}
