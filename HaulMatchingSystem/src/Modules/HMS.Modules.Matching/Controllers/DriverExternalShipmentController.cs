using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Driver External Shipment Declaration controller.
    /// Allows drivers to declare shipments encountered outside the system.
    /// 
    /// POST   /api/driver/external-shipments          — create declaration
    /// GET    /api/driver/external-shipments           — list own declarations
    /// GET    /api/driver/external-shipments/{id}      — detail of own declaration
    /// </summary>
    [ApiController]
    [Route("api/driver/external-shipments")]
    [Authorize(Roles = "Driver")]
    public class DriverExternalShipmentController : ControllerBase
    {
        private readonly IDriverExternalShipmentService _service;
        private readonly ILogger<DriverExternalShipmentController> _logger;

        public DriverExternalShipmentController(
            IDriverExternalShipmentService service,
            ILogger<DriverExternalShipmentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("Không thể xác định người dùng.");
            return userId;
        }

        /// <summary>
        /// Declare an external shipment. The active trip is auto-detected.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateExternalShipment(
            [FromBody] CreateExternalShipmentRequest request,
            CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _service.CreateExternalShipmentAsync(driverId, request, ct);
                return CreatedAtAction(
                    nameof(GetExternalShipmentDetail),
                    new { id = result.ProposalId },
                    result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating external shipment for driver");
                return StatusCode(500, new { message = "Lỗi khi khai báo hàng ngoài." });
            }
        }

        /// <summary>
        /// List driver's external shipments with optional status filter.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetExternalShipments(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _service.GetExternalShipmentsAsync(
                    driverId, status, page, pageSize, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting external shipments for driver");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách hàng ngoài." });
            }
        }

        /// <summary>
        /// Get detail of a specific external shipment declaration.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetExternalShipmentDetail(
            Guid id,
            CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _service.GetExternalShipmentDetailAsync(driverId, id, ct);
                if (result == null)
                    return NotFound(new { message = "Không tìm thấy khai báo hàng ngoài." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting external shipment detail {ProposalId}", id);
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết khai báo hàng ngoài." });
            }
        }
    }
}
