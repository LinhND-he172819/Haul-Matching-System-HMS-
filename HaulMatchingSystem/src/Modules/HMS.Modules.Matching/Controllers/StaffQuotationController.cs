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
    /// Staff quotation management controller.
    /// GET  /api/staff/quotations                              — list quotations (paged, filtered)
    /// GET  /api/staff/quotations/{quotationId}                 — get quotation detail
    /// POST /api/staff/proposals/{proposalId}/quotations        — create quotation
    /// PUT  /api/staff/quotations/{quotationId}                 — update quotation
    /// POST /api/staff/quotations/{quotationId}/send            — send quotation
    /// POST /api/staff/quotations/{quotationId}/cancel          — cancel quotation
    /// </summary>
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Admin,Warehouse_Staff")]
    public class StaffQuotationController : ControllerBase
    {
        private readonly IQuotationService _quotationService;
        private readonly IStaffProposalService _staffProposalService;
        private readonly ILogger<StaffQuotationController> _logger;

        public StaffQuotationController(
            IQuotationService quotationService,
            IStaffProposalService staffProposalService,
            ILogger<StaffQuotationController> logger)
        {
            _quotationService = quotationService;
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
        /// List quotations with optional status filter and pagination.
        /// </summary>
        [HttpGet("quotations")]
        public async Task<IActionResult> GetQuotations(
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

                var result = await _staffProposalService.GetQuotationsAsync(
                    staffId, role, hubId, status, page, pageSize, ct);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting staff quotations");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách báo giá." });
            }
        }

        /// <summary>
        /// Create a new quotation draft for an approved proposal.
        /// </summary>
        [HttpPost("proposals/{proposalId:guid}/quotations")]
        public async Task<IActionResult> CreateQuotation(
            Guid proposalId,
            [FromBody] CreateQuotationRequest request,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();
                var result = await _quotationService.CreateQuotationAsync(proposalId, request, staffId, role, hubId, ct);
                return CreatedAtAction(nameof(GetQuotation), new { quotationId = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                _logger.LogWarning(ex, "Forbidden creating quotation for proposal {ProposalId}", proposalId);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quotation for proposal {ProposalId}", proposalId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update a quotation in Draft status.
        /// </summary>
        [HttpPut("quotations/{quotationId:guid}")]
        public async Task<IActionResult> UpdateQuotation(
            Guid quotationId,
            [FromBody] UpdateQuotationRequest request,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();
                var result = await _quotationService.UpdateQuotationAsync(quotationId, request, staffId, role, hubId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                _logger.LogWarning(ex, "Forbidden updating quotation {QuotationId}", quotationId);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Send a quotation: Draft → Sent. Also transitions shipment to PendingDeposit.
        /// </summary>
        [HttpPost("quotations/{quotationId:guid}/send")]
        public async Task<IActionResult> SendQuotation(
            Guid quotationId,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();
                await _quotationService.SendQuotationAsync(quotationId, staffId, role, hubId, ct);
                return Ok(new { message = "Gửi báo giá thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                _logger.LogWarning(ex, "Forbidden sending quotation {QuotationId}", quotationId);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a quotation: Draft/Sent → Cancelled.
        /// </summary>
        [HttpPost("quotations/{quotationId:guid}/cancel")]
        public async Task<IActionResult> CancelQuotation(
            Guid quotationId,
            [FromBody] CancelQuotationRequest? request,
            CancellationToken ct)
        {
            try
            {
                var staffId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                var hubId = GetStaffHubId();
                await _quotationService.CancelQuotationAsync(quotationId, staffId, role, hubId, request?.Reason, ct);
                return Ok(new { message = "Hủy báo giá thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                _logger.LogWarning(ex, "Forbidden cancelling quotation {QuotationId}", quotationId);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get quotation detail by ID.
        /// </summary>
        [HttpGet("quotations/{quotationId:guid}")]
        public async Task<IActionResult> GetQuotation(
            Guid quotationId,
            CancellationToken ct)
        {
            try
            {
                var result = await _quotationService.GetQuotationAsync(quotationId, GetCurrentUserRole(), GetStaffHubId(), ct);
                if (result == null)
                    return NotFound(new { message = "Không tìm thấy báo giá." });
                return Ok(result);
            }
            catch (ForbiddenException ex)
            {
                _logger.LogWarning(ex, "Forbidden getting quotation {QuotationId}", quotationId);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request body for cancelling a quotation.
    /// </summary>
    public class CancelQuotationRequest
    {
        public string? Reason { get; set; }
    }
}
