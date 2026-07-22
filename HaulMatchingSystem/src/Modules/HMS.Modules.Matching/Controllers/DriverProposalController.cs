using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// API for Driver to view and respond to shipment proposals.
    /// </summary>
    [ApiController]
    [Route("api/driver/proposals")]
    [Authorize(Roles = "Driver")]
    public class DriverProposalController : ControllerBase
    {
        private readonly IProposalService _proposalService;
        private readonly ILogger<DriverProposalController> _logger;

        public DriverProposalController(IProposalService proposalService, ILogger<DriverProposalController> logger)
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

        /// <summary>
        /// Get all pending proposals for the current driver's active trip.
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingProposals(CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _proposalService.GetDriverPendingProposalsAsync(driverId, ct);

                if (result == null)
                    return NotFound(new { message = "Bạn chưa có chuyến đang hoạt động để nhận đề xuất." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending proposals for driver");
                return StatusCode(500, new { message = "Lỗi khi tải danh sách đề xuất: " + ex.Message });
            }
        }

        /// <summary>
        /// Accept a single proposal.
        /// </summary>
        [HttpPost("{proposalId:guid}/accept")]
        public async Task<IActionResult> AcceptProposal(Guid proposalId, CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _proposalService.AcceptProposalAsync(proposalId, driverId, ct);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Lỗi khi chấp nhận đề xuất: " + ex.Message });
            }
        }

        /// <summary>
        /// Reject a single proposal with a reason.
        /// </summary>
        [HttpPost("{proposalId:guid}/reject")]
        public async Task<IActionResult> RejectProposal(
            Guid proposalId,
            [FromBody] RejectProposalRequest request,
            CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _proposalService.RejectProposalAsync(proposalId, driverId, request, ct);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Lỗi khi từ chối đề xuất: " + ex.Message });
            }
        }

        /// <summary>
        /// Accept all pending proposals for the driver's active trip.
        /// </summary>
        [HttpPost("accept-all")]
        public async Task<IActionResult> AcceptAll(
            [FromBody] AcceptAllProposalsRequest request,
            CancellationToken ct)
        {
            try
            {
                var driverId = GetCurrentUserId();
                var result = await _proposalService.AcceptAllProposalsAsync(driverId, request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting all proposals");
                return StatusCode(500, new { message = "Lỗi khi chấp nhận tất cả đề xuất: " + ex.Message });
            }
        }
    }
}
