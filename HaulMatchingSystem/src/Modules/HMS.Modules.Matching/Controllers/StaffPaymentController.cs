using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Staff payment monitoring controller.
    /// GET /api/staff/payments — list payments (paged, filtered)
    /// </summary>
    [ApiController]
    [Route("api/staff/payments")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public class StaffPaymentController : ControllerBase
    {
        private readonly IStaffProposalService _staffProposalService;
        private readonly ILogger<StaffPaymentController> _logger;

        public StaffPaymentController(
            IStaffProposalService staffProposalService,
            ILogger<StaffPaymentController> logger)
        {
            _staffProposalService = staffProposalService;
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
    }
}
