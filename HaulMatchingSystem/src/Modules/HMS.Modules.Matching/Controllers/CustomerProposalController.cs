using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// API for Customer to create/cancel shipment proposals.
    /// POST /api/trip-posts/{tripPostId}/proposals     â€” create proposal
    /// DELETE /api/trip-posts/{tripPostId}/proposals/{proposalId} â€” cancel proposal
    /// </summary>
    [ApiController]
    [Route("api/trip-posts")]
    [Authorize(Roles = "Customer")]
    public class CustomerProposalController : ControllerBase
    {
        private readonly IProposalService _proposalService;
        private readonly ILogger<CustomerProposalController> _logger;

        public CustomerProposalController(IProposalService proposalService, ILogger<CustomerProposalController> logger)
        {
            _proposalService = proposalService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("KhÃ´ng thá»ƒ xÃ¡c Ä‘á»‹nh ngÆ°á»i dÃ¹ng hiá»‡n táº¡i.");
            return userId;
        }

        /// <summary>
        /// Create a new proposal to link a Shipment to a TripPost.
        /// </summary>
        [HttpPost("{tripPostId:guid}/proposals")]
        public async Task<IActionResult> CreateProposal(
            Guid tripPostId,
            [FromBody] CreateProposalRequest request,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _proposalService.CreateProposalAsync(tripPostId, customerId, request, ct);
                return CreatedAtAction(nameof(CreateProposal), new { tripPostId }, result);
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
                _logger.LogError(ex, "Error creating proposal for TripPost {TripPostId}", tripPostId);
                return StatusCode(500, new { message = "Loi tao de xuat: " + ex.Message });
            }
        }

        /// <summary>
        /// Cancel a pending proposal.
        /// </summary>
        [HttpDelete("{tripPostId:guid}/proposals/{proposalId:guid}")]
        public async Task<IActionResult> CancelProposal(
            Guid tripPostId,
            Guid proposalId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                await _proposalService.CancelProposalAsync(proposalId, customerId, ct);
                return NoContent();
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
                _logger.LogError(ex, "Error cancelling proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = "Loi huy de xuat: " + ex.Message });
            }
        }
    }
}
