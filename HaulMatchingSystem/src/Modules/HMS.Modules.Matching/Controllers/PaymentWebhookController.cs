using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Payment webhook controller for payment gateway callbacks.
    /// POST /api/payments/webhook — process payment callback
    /// </summary>
    [ApiController]
    [Route("api/payments")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentWebhookController> _logger;

        public PaymentWebhookController(
            IPaymentService paymentService,
            ILogger<PaymentWebhookController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Process payment webhook callback.
        /// This endpoint should be secured via signature verification in production.
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous] // Webhooks typically don't use JWT auth; secured via signature instead
        public async Task<IActionResult> ProcessWebhook(
            [FromBody] PaymentWebhookRequest webhook,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhook.TransactionReference))
                    return BadRequest(new { message = "TransactionReference là bắt buộc." });

                _logger.LogInformation(
                    "Payment webhook received: ref={TransactionReference}, status={Status}, amount={Amount}",
                    webhook.TransactionReference, webhook.Status, webhook.Amount);

                await _paymentService.ProcessWebhookAsync(webhook, ct);

                return Ok(new { message = "Webhook processed successfully." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid webhook: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment webhook");
                return StatusCode(500, new { message = "Lỗi khi xử lý webhook thanh toán." });
            }
        }
    }
}
