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
    /// Customer quotation and payment controller.
    /// GET    /api/customer/quotations/{quotationId}                    — view quotation
    /// POST   /api/customer/quotations/{quotationId}/deposit-payment    — create deposit payment
    /// POST   /api/customer/quotations/{quotationId}/final-payment      — create final payment
    /// GET    /api/customer/shipments/{shipmentId}/payments             — payment summary
    /// GET    /api/customer/quotations/{quotationId}/payment-history    — payment history
    /// POST   /api/customer/payments/{paymentId}/retry                  — retry failed payment (Part 1)
    /// POST   /api/customer/payments/{paymentId}/cancel                 — cancel pending payment (Part 3)
    /// GET    /api/customer/payments/{paymentId}                        — payment detail (Part 5)
    /// GET    /api/customer/payments/{paymentId}/timeline               — payment timeline (Part 6)
    /// </summary>
    [ApiController]
    [Route("api/customer")]
    [Authorize(Roles = "Customer")]
    public class CustomerQuotationController : ControllerBase
    {
        private readonly IQuotationService _quotationService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<CustomerQuotationController> _logger;

        public CustomerQuotationController(
            IQuotationService quotationService,
            IPaymentService paymentService,
            ILogger<CustomerQuotationController> logger)
        {
            _quotationService = quotationService;
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

        /// <summary>
        /// Get quotation detail (customer view, ownership verified).
        /// </summary>
        [HttpGet("quotations/{quotationId:guid}")]
        public async Task<IActionResult> GetQuotation(
            Guid quotationId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _quotationService.GetQuotationForCustomerAsync(quotationId, customerId, ct);
                if (result == null)
                    return NotFound(new { message = "Không tìm thấy báo giá." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quotation for customer");
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin báo giá." });
            }
        }

        /// <summary>
        /// Create deposit payment for a quotation.
        /// </summary>
        [HttpPost("quotations/{quotationId:guid}/deposit-payment")]
        public async Task<IActionResult> CreateDepositPayment(
            Guid quotationId,
            [FromBody] CreateDepositPaymentRequest request,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.CreateDepositPaymentAsync(quotationId, customerId, request, ct);
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
                _logger.LogError(ex, "Error creating deposit payment for quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = "Lỗi khi tạo thanh toán cọc." });
            }
        }

        /// <summary>
        /// Create final payment for a quotation (after delivery).
        /// </summary>
        [HttpPost("quotations/{quotationId:guid}/final-payment")]
        public async Task<IActionResult> CreateFinalPayment(
            Guid quotationId,
            [FromBody] CreateFinalPaymentRequest request,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.CreateFinalPaymentAsync(quotationId, customerId, request, ct);
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
                _logger.LogError(ex, "Error creating final payment for quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = "Lỗi khi tạo thanh toán cuối." });
            }
        }

        /// <summary>
        /// Get payment summary for a shipment (ownership verified).
        /// </summary>
        [HttpGet("shipments/{shipmentId:guid}/payments")]
        public async Task<IActionResult> GetPaymentSummary(
            Guid shipmentId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.GetPaymentSummaryAsync(shipmentId, customerId, ct);
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
                _logger.LogError(ex, "Error getting payment summary for shipment {ShipmentId}", shipmentId);
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin thanh toán." });
            }
        }

        /// <summary>
        /// Get payment history for a quotation (ownership verified).
        /// </summary>
        [HttpGet("quotations/{quotationId:guid}/payment-history")]
        public async Task<IActionResult> GetPaymentHistory(
            Guid quotationId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.GetPaymentHistoryAsync(quotationId, customerId, ct);
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
                _logger.LogError(ex, "Error getting payment history for quotation {QuotationId}", quotationId);
                return StatusCode(500, new { message = "Lỗi khi lấy lịch sử thanh toán." });
            }
        }

        // ═══ Part 1: Retry Payment ═══

        /// <summary>
        /// Retry a failed payment: Failed → Pending. Keeps payment_code.
        /// </summary>
        [HttpPost("payments/{paymentId:guid}/retry")]
        public async Task<IActionResult> RetryPayment(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.RetryPaymentAsync(paymentId, customerId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi thử lại thanh toán." });
            }
        }

        // ═══ Part 3: Cancel Payment ═══

        /// <summary>
        /// Cancel a pending payment: Pending → Cancelled. Can retry later.
        /// </summary>
        [HttpPost("payments/{paymentId:guid}/cancel")]
        public async Task<IActionResult> CancelPayment(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.CancelPaymentAsync(paymentId, customerId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Lỗi khi hủy thanh toán." });
            }
        }

        // ═══ Part 5: Payment Detail ═══

        /// <summary>
        /// Get full payment detail (customer ownership verified).
        /// </summary>
        [HttpGet("payments/{paymentId:guid}")]
        public async Task<IActionResult> GetPaymentDetail(
            Guid paymentId,
            CancellationToken ct)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var result = await _paymentService.GetPaymentDetailAsync(paymentId, customerId, null, null, null, ct);
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
        [HttpGet("payments/{paymentId:guid}/timeline")]
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
    }
}
