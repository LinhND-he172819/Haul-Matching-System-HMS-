using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Controllers
{
    /// <summary>
    /// Mock Payment Controller — dev/test only.
    /// Provides endpoints to simulate payment success, failure, and cancellation
    /// without a real payment gateway.
    ///
    /// All endpoints are guarded by ASPNETCORE_ENVIRONMENT=Development OR
    /// config flag Payment:MockGatewayEnabled.
    ///
    /// POST /api/customer/payments/{paymentId}/mock-checkout  — open checkout session
    /// POST /api/customer/payments/{paymentId}/simulate       — simulate payment result
    /// POST /api/dev/payments/{paymentId}/simulate             — staff/anonymous simulate
    /// </summary>
    [ApiController]
    public class MockPaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MockPaymentController> _logger;

        public MockPaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            ILogger<MockPaymentController> logger)
        {
            _paymentService = paymentService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Check if mock payment gateway is enabled.
        /// Returns true when ASPNETCORE_ENVIRONMENT=Development OR Payment:MockGatewayEnabled is true.
        /// </summary>
        private bool IsMockEnabled()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? "Production";
            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)) return true;
            return _configuration.GetValue<bool>("Payment:MockGatewayEnabled");
        }

        private void EnsureMockEnabled()
        {
            if (!IsMockEnabled())
                throw new InvalidOperationException("Mock payment gateway is disabled in this environment.");
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("Không thể xác định người dùng hiện tại.");
            return userId;
        }

        /// <summary>
        /// Open a mock checkout session for a Pending payment.
        /// Returns checkout info including a CheckoutSessionId and ExpiresAt.
        /// Customer only — verifies ownership.
        /// </summary>
        [HttpPost("api/customer/payments/{paymentId:guid}/mock-checkout")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> OpenMockCheckout(
            Guid paymentId,
            [FromBody] MockCheckoutRequest request,
            CancellationToken ct)
        {
            try
            {
                EnsureMockEnabled();

                var customerId = GetCurrentUserId();

                // Get payment detail to verify ownership and check status
                var detail = await _paymentService.GetPaymentDetailAsync(
                    paymentId, customerId, null, null, null, ct);

                if (detail == null)
                    return NotFound(new { message = "Không tìm thấy thanh toán." });

                if (detail.CustomerId != customerId)
                    return Forbid();

                if (detail.Status != "Pending")
                    return BadRequest(new { message = $"Thanh toán không ở trạng thái Pending. Hiện tại: {detail.Status}" });

                if (detail.AllowedActions?.CanContinuePayment != true)
                    return BadRequest(new { message = "Thanh toán này không thể tiếp tục." });

                // Generate a mock checkout session
                var checkoutSessionId = Guid.NewGuid();

                // Update payment_method if provided
                if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
                {
                    await using var conn = new Npgsql.NpgsqlConnection(
                        _configuration.GetConnectionString("DefaultConnection")
                        ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123");
                    await conn.OpenAsync(ct);
                    const string updateMethodSql = """
                        UPDATE warehouse.payments
                        SET payment_method = @method, updated_at = NOW()
                        WHERE id = @id AND is_deleted = FALSE;
                    """;
                    await using var cmd = new Npgsql.NpgsqlCommand(updateMethodSql, conn);
                    cmd.Parameters.AddWithValue("id", paymentId);
                    cmd.Parameters.AddWithValue("method", request.PaymentMethod);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                _logger.LogInformation(
                    "Mock checkout opened: paymentId={PaymentId}, session={SessionId}, method={Method}",
                    paymentId, checkoutSessionId, request.PaymentMethod);

                return Ok(new MockCheckoutResponse
                {
                    PaymentId = paymentId,
                    CheckoutSessionId = checkoutSessionId,
                    CheckoutUrl = $"/mock-checkout/{checkoutSessionId}",
                    ExpiresAt = detail.ExpiresAt ?? DateTime.UtcNow.AddMinutes(60)
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening mock checkout");
                return StatusCode(500, new { message = "Lỗi khi mở phiên thanh toán mô phỏng." });
            }
        }

        /// <summary>
        /// Simulate a payment result (Customer endpoint).
        /// Calls ProcessWebhookAsync with a generated TransactionReference.
        /// Customer only — verifies ownership.
        /// </summary>
        [HttpPost("api/customer/payments/{paymentId:guid}/simulate")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> SimulatePaymentAsCustomer(
            Guid paymentId,
            [FromBody] SimulatePaymentRequest request,
            CancellationToken ct)
        {
            try
            {
                EnsureMockEnabled();

                var customerId = GetCurrentUserId();

                // Get payment detail to verify ownership
                var detail = await _paymentService.GetPaymentDetailAsync(
                    paymentId, customerId, null, null, null, ct);

                if (detail == null)
                    return NotFound(new { message = "Không tìm thấy thanh toán." });

                if (detail.CustomerId != customerId)
                    return Forbid();

                if (detail.Status != "Pending")
                    return BadRequest(new { message = $"Thanh toán không ở trạng thái Pending. Hiện tại: {detail.Status}" });

                // Build mock webhook request
                var webhook = await BuildMockWebhook(detail, request.Result, ct);

                _logger.LogInformation(
                    "Simulating payment: paymentId={PaymentId}, result={Result}, ref={Ref}",
                    paymentId, request.Result, webhook.TransactionReference);

                // Process through the same webhook pipeline
                await _paymentService.ProcessWebhookAsync(webhook, ct);

                return Ok(new
                {
                    message = $"Đã mô phỏng thanh toán: {request.Result}",
                    paymentId,
                    result = request.Result,
                    transactionReference = webhook.TransactionReference
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating payment for customer");
                return StatusCode(500, new { message = "Lỗi khi mô phỏng thanh toán." });
            }
        }

        /// <summary>
        /// Simulate a payment result (Dev/Staff endpoint).
        /// No authentication required in Development; role-based in other envs.
        /// </summary>
        [HttpPost("api/dev/payments/{paymentId:guid}/simulate")]
        [Authorize(Roles = "Admin,Admin_Staff,Warehouse_Staff")]
        public async Task<IActionResult> SimulatePaymentAsStaff(
            Guid paymentId,
            [FromBody] SimulatePaymentRequest request,
            CancellationToken ct)
        {
            try
            {
                EnsureMockEnabled();

                // Staff can simulate any payment (no customer ownership check)
                var detail = await _paymentService.GetPaymentDetailAsync(
                    paymentId, null, null, null, null, ct);

                if (detail == null)
                    return NotFound(new { message = "Không tìm thấy thanh toán." });

                if (detail.Status != "Pending")
                    return BadRequest(new { message = $"Thanh toán không ở trạng thái Pending. Hiện tại: {detail.Status}" });

                var webhook = await BuildMockWebhook(detail, request.Result, ct);

                _logger.LogInformation(
                    "Staff simulating payment: paymentId={PaymentId}, result={Result}, ref={Ref}",
                    paymentId, request.Result, webhook.TransactionReference);

                await _paymentService.ProcessWebhookAsync(webhook, ct);

                return Ok(new
                {
                    message = $"Đã mô phỏng thanh toán: {request.Result}",
                    paymentId,
                    result = request.Result,
                    transactionReference = webhook.TransactionReference
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating payment for staff");
                return StatusCode(500, new { message = "Lỗi khi mô phỏng thanh toán." });
            }
        }

        /// <summary>
        /// Build a PaymentWebhookRequest that matches the payment's existing transaction_reference.
        /// If the payment has no transaction_reference yet, generates one and updates the DB.
        /// </summary>
        private async Task<PaymentWebhookRequest> BuildMockWebhook(
            PaymentDetailDto detail, string result, CancellationToken ct)
        {
            // Determine the result status for the webhook
            var webhookStatus = result.ToLowerInvariant() switch
            {
                "paid" or "success" => "Paid",
                "failed" or "failure" => "Failed",
                "cancelled" or "canceled" => "Failed", // Treat cancel as failure for webhook processing
                _ => throw new InvalidOperationException(
                    $"Result không hợp lệ: {result}. Chọn: Paid, Failed, hoặc Cancelled.")
            };

            // Use existing transaction_reference if present, or generate a mock one
            var transactionRef = detail.TransactionReference;
            if (string.IsNullOrWhiteSpace(transactionRef))
            {
                transactionRef = $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

                // Update the payment with the generated transaction reference
                await using var conn = new Npgsql.NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")
                    ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123");
                await conn.OpenAsync(ct);

                const string updateRefSql = """
                    UPDATE warehouse.payments
                    SET transaction_reference = @ref, updated_at = NOW()
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                await using var cmd = new Npgsql.NpgsqlCommand(updateRefSql, conn);
                cmd.Parameters.AddWithValue("id", detail.Id);
                cmd.Parameters.AddWithValue("ref", transactionRef);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return new PaymentWebhookRequest
            {
                TransactionReference = transactionRef,
                Status = webhookStatus,
                Amount = detail.Amount,
                Currency = detail.Currency,
                PaidAt = DateTime.UtcNow,
                PaymentMethod = detail.PaymentMethod ?? "MockBanking"
            };
        }
    }
}
