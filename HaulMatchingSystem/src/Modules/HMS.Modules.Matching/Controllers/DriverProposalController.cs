using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public DriverProposalController(IProposalService proposalService)
        {
            _proposalService = proposalService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("KhÃ´ng thá»ƒ xÃ¡c Ä‘á»‹nh ngÆ°á»i dÃ¹ng hiá»‡n táº¡i.");
            return userId;
        }

        /// <summary>
        /// Get all pending proposals for the current driver's active trip.
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingProposals(CancellationToken ct)
        {
            var driverId = GetCurrentUserId();
            var result = await _proposalService.GetDriverPendingProposalsAsync(driverId, ct);

            if (result == null)
                return NotFound(new { message = "Báº¡n chÆ°a cÃ³ chuyáº¿n Ä‘ang hoáº¡t Ä‘á»™ng Ä‘á»ƒ nháº­n Ä‘á» xuáº¥t." });

            return Ok(result);
        }

        /// <summary>
        /// Accept a single proposal.
        /// </summary>
        [HttpPost("{proposalId:guid}/accept")]
        public async Task<IActionResult> AcceptProposal(Guid proposalId, CancellationToken ct)
        {
            var driverId = GetCurrentUserId();
            var result = await _proposalService.AcceptProposalAsync(proposalId, driverId, ct);
            return Ok(result);
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
            var driverId = GetCurrentUserId();
            var result = await _proposalService.RejectProposalAsync(proposalId, driverId, request, ct);
            return Ok(result);
        }

        /// <summary>
        /// Accept all pending proposals for the driver's active trip.
        /// </summary>
        [HttpPost("accept-all")]
        public async Task<IActionResult> AcceptAll(
            [FromBody] AcceptAllProposalsRequest request,
            CancellationToken ct)
        {
            var driverId = GetCurrentUserId();
            var result = await _proposalService.AcceptAllProposalsAsync(driverId, request, ct);
            return Ok(result);
        }
    }
}
