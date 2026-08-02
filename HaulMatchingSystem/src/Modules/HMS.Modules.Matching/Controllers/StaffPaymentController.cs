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
    /// Staff payment monitoring controller.
    /// GET    /api/staff/payments                        — list payments (paged, filtered)
    /// GET    /api/staff/payments/{paymentId}            — payment detail (Part 5)
    /// GET    /api/staff/payments/{paymentId}/timeline   — payment timeline (Part 6)
    /// POST   /api/staff/payments/{paymentId}/refund     — request refund (Part 7)
    /// POST   /api/staff/payments/{paymentId}/refund/approve — approve refund (Part 7)
    /// </summary>
    [ApiController]
    [Route("api/staff/payments")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public class StaffPaymentController : ControllerBase
    {
        private readonly IStaffProposalService _staffProposalService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<StaffPaymentController> _logger;

        public StaffPaymentController(
            IStaffProposalService staffProposalService,
            IPaymentService paymentService,
            ILogger<StaffPaymentController> logger)
        {
            _staffProposalService = staffProposalService;
            _paymentService = paymentService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("Không thể xác định người dùng hiện tại.");
            return userId;
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        private Guid? GetStaffHubId()
        {
            var hubClaim = User.FindFirst("HubId");
            if (hubClaim != null && Guid.TryParse(hubClaim.Value, out var hubId))
                return hubId;
            return null;
        }

        /// <summary>
        /// List payments with optional status filter and pagination.
        /// Warehouse_Staff sees only payments for their hub.
        /// Admin sees all.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPayments(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                var result = await _staffProposalService.GetPaymentsAsync(
                    staffId, role, hubId, status, page, pageSize, ct);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting staff payments");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách thanh toán." });
            }
        }

        // ═══ Part 5: Payment Detail ═══

        /// <summary>
        /// Get payment detail. Admin sees all. Warehouse_Staff sees only their hub's payments.
        /// </summary>
        [HttpGet("{paymentId:guid}")]
        public async Task<IActionResult> GetPaymentDetail(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                var result = await _paymentService.GetStaffPaymentDetailAsync(paymentId, staffId, role, hubId, ct);
                if (result == null)
                    return NotFound(new { message = "Không tìm thấy thanh toán." });
                return Ok(result);
            }
            catch (ForbiddenException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment detail {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin thanh toán." });
            }
        }

        // ═══ Part 6: Payment Timeline ═══

        /// <summary>
        /// Get payment timeline (status history from audit log).
        /// </summary>
        [HttpGet("{paymentId:guid}/timeline")]
        public async Task<IActionResult> GetPaymentTimeline(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var result = await _paymentService.GetPaymentTimelineAsync(paymentId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment timeline {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi lấy lịch sử trạng thái thanh toán." });
            }
        }

        // ═══ Part 7: Refund ═══

        /// <summary>
        /// Request refund: Paid → PendingRefund.
        /// </summary>
        [HttpPost("{paymentId:guid}/refund")]
        public async Task<IActionResult> RequestRefund(
            Guid paymentId,
            [FromBody] RequestRefundRequest request,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var result = await _paymentService.RequestRefundAsync(paymentId, staffId, request.Reason, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting refund for {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi yêu cầu hoàn tiền." });
            }
        }

        /// <summary>
        /// Approve refund (Admin only): PendingRefund → Refunded.
        /// If deposit refund → Shipment Matched → PendingReview, Quotation → Cancelled.
        /// </summary>
        [HttpPost("{paymentId:guid}/refund/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveRefund(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var result = await _paymentService.ApproveRefundAsync(paymentId, staffId, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized refund approval attempt for {PaymentId}", paymentId);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving refund for {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi duyệt hoàn tiền." });
            }
        }
    }
}
