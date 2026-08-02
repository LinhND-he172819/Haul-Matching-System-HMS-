using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Driver COD payment confirmation controller.
    /// POST /api/driver/payments/{paymentId}/confirm-cod — confirm COD payment received
    /// </summary>
    [ApiController]
    [Route("api/driver/payments")]
    [Authorize(Roles = "Driver")]
    public class DriverCODController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<DriverCODController> _logger;

        public DriverCODController(
            IPaymentService paymentService,
            ILogger<DriverCODController> logger)
        {
            _paymentService = paymentService;
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
        /// Confirm COD payment received: Pending → Paid (for COD FinalPayment).
        /// Driver collects cash from customer and confirms. Then shipment Delivered → Completed.
        /// </summary>
        [HttpPost("{paymentId:guid}/confirm-cod")]
        public async Task<IActionResult> ConfirmCodPayment(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _paymentService.ConfirmCodPaymentAsync(paymentId, driverId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (ForbiddenException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming COD payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi xác nhận thanh toán COD." });
            }
        }
    }
}
