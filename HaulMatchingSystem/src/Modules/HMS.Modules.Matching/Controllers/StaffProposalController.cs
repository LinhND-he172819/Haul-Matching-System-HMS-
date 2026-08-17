using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Staff proposal management controller.
    /// GET  /api/staff/proposals                    — list proposals (paged, filtered)
    /// GET  /api/staff/proposals/{proposalId}       — get proposal detail
    /// POST /api/staff/proposals/{proposalId}/approve — approve proposal
    /// POST /api/staff/proposals/{proposalId}/reject  — reject proposal with reason
    /// </summary>
    [ApiController]
    [Route("api/staff/proposals")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public class StaffProposalController : ControllerBase
    {
        private readonly IStaffProposalService _proposalService;
        private readonly ILogger<StaffProposalController> _logger;

        public StaffProposalController(
            IStaffProposalService proposalService,
            ILogger<StaffProposalController> logger)
        {
            _proposalService = proposalService;
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
        /// List proposals with optional status/source/driver filter and pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProposals(
            [FromQuery] string? status,
            [FromQuery] string? proposalSource,
            [FromQuery] Guid? driverId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                var result = await _proposalService.GetProposalsAsync(
                    staffId, role, hubId, status, proposalSource, driverId, search, page, pageSize, ct);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting staff proposals: {Type} - {Message}", ex.GetType().Name, ex.Message);
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách đề xuất.", detail = ex.Message, type = ex.GetType().Name });
            }
        }

        /// <summary>
        /// Get proposal detail by ID.
        /// </summary>
        [HttpGet("{proposalId:guid}")]
        public async Task<IActionResult> GetProposalDetail(
            Guid proposalId,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                var result = await _proposalService.GetProposalDetailAsync(
                    proposalId, staffId, role, hubId, ct);

                if (result == null)
                    return NotFound(new { message = "Không tìm thấy đề xuất." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting proposal detail {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết đề xuất." });
            }
        }

        /// <summary>
        /// Approve a proposal.
        /// </summary>
        [HttpPost("{proposalId:guid}/approve")]
        public async Task<IActionResult> ApproveProposal(
            Guid proposalId,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                await _proposalService.ApproveProposalAsync(proposalId, staffId, role, hubId, ct);

                return Ok(new { message = "Duyệt đề xuất thành công." });
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
                _logger.LogError(ex, "Error approving proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Lỗi khi duyệt đề xuất." });
            }
        }

        /// <summary>
        /// Reject a proposal with a reason.
        /// </summary>
        [HttpPost("{proposalId:guid}/reject")]
        public async Task<IActionResult> RejectProposal(
            Guid proposalId,
            [FromBody] RejectProposalRequestByStaff request,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();

                await _proposalService.RejectProposalAsync(
                    proposalId, staffId, role, hubId, request.Reason, ct);

                return Ok(new { message = "Từ chối đề xuất thành công." });
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
                _logger.LogError(ex, "Error rejecting proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Lỗi khi từ chối đề xuất." });
            }
        }
    }
}
