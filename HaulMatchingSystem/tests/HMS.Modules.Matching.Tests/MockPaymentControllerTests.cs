using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Controllers;
using HMS.Modules.Matching.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HMS.Modules.Matching.Tests;

/// <summary>
/// Unit tests for MockPaymentController.
/// Covers: environment guard, ownership checks, status validation,
/// endpoint responses, and webhook result mapping.
/// </summary>
public class MockPaymentControllerTests : IDisposable
{
    private readonly Mock<IPaymentService> _paymentService = new();
    private readonly Mock<ILogger<MockPaymentController>> _logger = new();
    private readonly string _origEnv;

    // Simulate a working connection string for BuildMockWebhook DB updates
    private const string TestConnectionString = "Host=localhost;Database=test_db;Username=test;Password=test";

    public MockPaymentControllerTests()
    {
        // Save and clear environment to ensure deterministic IsMockEnabled behavior
        _origEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _origEnv);
    }

    /* ─── Helpers ─────────────────────────────────────────────────── */

    /// <summary>
    /// Build an IConfiguration that returns the specified MockGatewayEnabled value
    /// and provides a connection string.
    /// </summary>
    private static IConfiguration BuildConfig(bool mockEnabled = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MockGatewayEnabled"] = mockEnabled.ToString(),
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            })
            .Build();

    /// <summary>
    /// Build a ClaimsPrincipal with the given userId and role.
    /// </summary>
    private static ClaimsPrincipal BuildUser(Guid userId, string role = "Customer") =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));

    /// <summary>
    /// Create a MockPaymentController with full HttpContext set up.
    /// </summary>
    private MockPaymentController CreateController(
        IConfiguration? config = null,
        ClaimsPrincipal? user = null)
    {
        var controller = new MockPaymentController(
            _paymentService.Object,
            config ?? BuildConfig(),
            _logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? BuildUser(Guid.NewGuid())
            }
        };

        return controller;
    }

    /// <summary>
    /// Create a minimal PaymentDetailDto for mocking service responses.
    /// </summary>
    private static PaymentDetailDto MakeDetail(
        Guid? id = null,
        Guid? customerId = null,
        string status = "Pending",
        string paymentType = "Deposit",
        decimal amount = 500_000m,
        string? transactionReference = null,
        bool canContinue = true,
        bool canCancel = true) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            PaymentCode = "PAY-TEST-001",
            Status = status,
            PaymentType = paymentType,
            Amount = amount,
            Currency = "VND",
            PaymentMethod = "MockBanking",
            CustomerId = customerId ?? Guid.NewGuid(),
            CustomerName = "Test Customer",
            QuotationId = Guid.NewGuid(),
            ShippingFee = 1_000_000m,
            DepositAmount = 500_000m,
            TransactionReference = transactionReference,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(50),
            AllowedActions = new PaymentAllowedActions
            {
                CanContinuePayment = canContinue,
                CanCancel = canCancel,
                CanRetry = false,
                CanViewDetail = true
            },
            Timeline = new List<PaymentTimelineEntry>()
        };

    /* ═══════════════════════════════════════════════════════════════
       Section A: IsMockEnabled / EnsureMockEnabled Guard
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task OpenMockCheckout_WhenMockDisabled_Returns400()
    {
        // Arrange: ASPNETCORE_ENVIRONMENT=Production and no config flag
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var config = BuildConfig(mockEnabled: false);
        var controller = CreateController(config: config);

        // Act
        var result = await controller.OpenMockCheckout(
            Guid.NewGuid(),
            new MockCheckoutRequest { PaymentMethod = "MockBanking" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Mock payment gateway is disabled", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SimulateAsCustomer_WhenMockDisabled_Returns400()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var config = BuildConfig(mockEnabled: false);
        var userId = Guid.NewGuid();
        var controller = CreateController(config: config, user: BuildUser(userId));

        // Act
        var result = await controller.SimulatePaymentAsCustomer(
            Guid.NewGuid(),
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SimulateAsStaff_WhenMockDisabled_Returns400()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var config = BuildConfig(mockEnabled: false);
        var controller = CreateController(config: config, user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            Guid.NewGuid(),
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task OpenMockCheckout_WhenConfigFlagEnabled_Returns200()
    {
        // Arrange: ASPNETCORE_ENVIRONMENT=Production but config flag is true
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var config = BuildConfig(mockEnabled: true);
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId);

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(config: config, user: BuildUser(userId));

        // Act — use null PaymentMethod to skip DB update in unit test
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = null },
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MockCheckoutResponse>(okResult.Value);
        Assert.Equal(paymentId, response.PaymentId);
        Assert.NotEqual(Guid.Empty, response.CheckoutSessionId);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section B: OpenMockCheckout — Ownership & Status Checks
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task OpenMockCheckout_PaymentNotFound_Returns404()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDetailDto?)null);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = "MockBanking" },
            CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task OpenMockCheckout_WrongOwner_Returns403()
    {
        // Arrange: payment belongs to customerA, but customerB is calling
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: ownerId);

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, callerId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(callerId));

        // Act
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = "MockBanking" },
            CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task OpenMockCheckout_NotPending_Returns400()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId, status: "Paid");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = "MockBanking" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Pending", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task OpenMockCheckout_CannotContinue_Returns400()
    {
        // Arrange: payment is Pending but AllowedActions.CanContinuePayment = false
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(
            id: paymentId, customerId: userId, status: "Pending",
            canContinue: false, canCancel: false);

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = "MockBanking" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không thể tiếp tục", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task OpenMockCheckout_Success_ReturnsCheckoutSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId);

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act — use null PaymentMethod to skip DB update in unit test
        var result = await controller.OpenMockCheckout(
            paymentId,
            new MockCheckoutRequest { PaymentMethod = null },
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MockCheckoutResponse>(okResult.Value);
        Assert.Equal(paymentId, response.PaymentId);
        Assert.NotEqual(Guid.Empty, response.CheckoutSessionId);
        Assert.StartsWith("/mock-checkout/", response.CheckoutUrl);
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section C: SimulatePaymentAsCustomer — All Flows
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task SimulateAsCustomer_PaymentNotFound_Returns404()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDetailDto?)null);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SimulateAsCustomer_WrongOwner_Returns403()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: ownerId, transactionReference: "EXISTING-REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, callerId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(callerId));

        // Act
        var result = await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task SimulateAsCustomer_NotPending_Returns400()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId, status: "Paid", transactionReference: "EXISTING-REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Pending", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SimulateAsCustomer_InvalidResult_Returns400()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId, transactionReference: "EXISTING-REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        var result = await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "InvalidResult" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không hợp lệ", badRequest.Value?.ToString());
    }

    /* ═══════════════════════════════════════════════════════════════
       Section D: SimulatePaymentAsStaff — All Flows
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task SimulateAsStaff_PaymentNotFound_Returns404()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDetailDto?)null);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SimulateAsStaff_NotPending_Returns400()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, status: "Paid", transactionReference: "EXISTING-REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SimulateAsStaff_InvalidResult_Returns400()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "EXISTING-REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin_Staff"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Garbage" },
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không hợp lệ", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SimulateAsStaff_CallsProcessWebhook_WithCorrectStatus()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "EXISTING-REF-123");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        PaymentWebhookRequest? capturedWebhook = null;
        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentWebhookRequest, CancellationToken>((wh, _) => capturedWebhook = wh)
            .Returns(Task.CompletedTask);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Warehouse_Staff"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(capturedWebhook);
        Assert.Equal("Paid", capturedWebhook!.Status);
        Assert.Equal("EXISTING-REF-123", capturedWebhook.TransactionReference);
        Assert.Equal(detail.Amount, capturedWebhook.Amount);
        Assert.Equal(detail.Currency, capturedWebhook.Currency);
        _paymentService.Verify(s => s.ProcessWebhookAsync(
            It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section E: BuildMockWebhook Result Mapping (via simulate endpoints)
       ═══════════════════════════════════════════════════════════════ */

    [Theory]
    [InlineData("paid", "Paid")]
    [InlineData("Paid", "Paid")]
    [InlineData("SUCCESS", "Paid")]
    [InlineData("success", "Paid")]
    public async Task BuildMockWebhook_PaidResult_MapsCorrectly(string input, string expected)
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "REF-PAID");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        PaymentWebhookRequest? captured = null;
        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentWebhookRequest, CancellationToken>((wh, _) => captured = wh)
            .Returns(Task.CompletedTask);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = input },
            CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expected, captured!.Status);
    }

    [Theory]
    [InlineData("failed", "Failed")]
    [InlineData("Failed", "Failed")]
    [InlineData("FAILURE", "Failed")]
    [InlineData("failure", "Failed")]
    public async Task BuildMockWebhook_FailedResult_MapsCorrectly(string input, string expected)
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "REF-FAIL");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        PaymentWebhookRequest? captured = null;
        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentWebhookRequest, CancellationToken>((wh, _) => captured = wh)
            .Returns(Task.CompletedTask);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = input },
            CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expected, captured!.Status);
    }

    [Theory]
    [InlineData("cancelled", "Failed")]
    [InlineData("canceled", "Failed")]
    [InlineData("Cancelled", "Failed")]
    public async Task BuildMockWebhook_CancelledResult_MapsToFailed(string input, string expected)
    {
        // Arrange — cancelled maps to "Failed" for webhook processing
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "REF-CANCEL");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        PaymentWebhookRequest? captured = null;
        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentWebhookRequest, CancellationToken>((wh, _) => captured = wh)
            .Returns(Task.CompletedTask);

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = input },
            CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expected, captured!.Status);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section F: Authorization Roles
       ═══════════════════════════════════════════════════════════════ */

    [Theory]
    [InlineData("Admin")]
    [InlineData("Admin_Staff")]
    [InlineData("Warehouse_Staff")]
    public async Task SimulateAsStaff_ValidRoles_CanCallEndpoint(string role)
    {
        // Arrange: staff roles should be able to reach the endpoint logic
        var paymentId = Guid.NewGuid();
        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDetailDto?)null); // Returns null to short-circuit

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), role));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert — should get 404 (not 403), confirming the role is accepted
        Assert.IsType<NotFoundObjectResult>(result);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section G: Verify ProcessWebhookAsync Not Called When Short-Circuited
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task SimulateAsCustomer_NotPending_DoesNotCallProcessWebhook()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: userId, status: "Paid", transactionReference: "REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, userId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(userId));

        // Act
        await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        _paymentService.Verify(s => s.ProcessWebhookAsync(
            It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimulateAsCustomer_WrongOwner_DoesNotCallProcessWebhook()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, customerId: ownerId, transactionReference: "REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, callerId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var controller = CreateController(user: BuildUser(callerId));

        // Act
        await controller.SimulatePaymentAsCustomer(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert
        _paymentService.Verify(s => s.ProcessWebhookAsync(
            It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section H: DTO Shape Tests
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public void MockCheckoutResponse_HasRequiredFields()
    {
        // Arrange & Act
        var response = new MockCheckoutResponse
        {
            PaymentId = Guid.NewGuid(),
            CheckoutSessionId = Guid.NewGuid(),
            CheckoutUrl = "/mock-checkout/test",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Assert
        Assert.NotEqual(Guid.Empty, response.PaymentId);
        Assert.NotEqual(Guid.Empty, response.CheckoutSessionId);
        Assert.NotNull(response.CheckoutUrl);
        Assert.True(response.ExpiresAt > DateTime.MinValue);
    }

    [Fact]
    public void SimulatePaymentRequest_HasResultField()
    {
        // Arrange & Act
        var request = new SimulatePaymentRequest { Result = "Paid" };

        // Assert
        Assert.Equal("Paid", request.Result);
    }

    [Fact]
    public void MockCheckoutRequest_HasPaymentMethodField()
    {
        // Arrange & Act
        var request = new MockCheckoutRequest { PaymentMethod = "MockWallet" };

        // Assert
        Assert.Equal("MockWallet", request.PaymentMethod);
    }

    [Fact]
    public void PaymentDetailDto_AllowedActions_DefaultsAreSet()
    {
        // Arrange & Act
        var detail = MakeDetail();

        // Assert
        Assert.NotNull(detail.AllowedActions);
        Assert.True(detail.AllowedActions!.CanContinuePayment);
        Assert.True(detail.AllowedActions.CanCancel);
        Assert.True(detail.AllowedActions.CanViewDetail);
    }

    [Fact]
    public void PaymentDetailDto_ExpiresAt_Computed()
    {
        // Arrange & Act
        var detail = MakeDetail();

        // Assert
        Assert.NotNull(detail.ExpiresAt);
        Assert.True(detail.ExpiresAt!.Value > DateTime.UtcNow);
    }

    /* ═══════════════════════════════════════════════════════════════
       Section I: ProcessWebhookAsync Exception Handling
       ═══════════════════════════════════════════════════════════════ */

    [Fact]
    public async Task SimulateAsStaff_ProcessWebhookThrowsInvalidOperationException_Returns400()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payment already processed"));

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Paid" },
            CancellationToken.None);

        // Assert — InvalidOperationException is caught and returns 400 BadRequest
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Payment already processed", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SimulateAsStaff_ServiceThrowsGenericException_Returns500()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var detail = MakeDetail(id: paymentId, transactionReference: "REF");

        _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        _paymentService.Setup(s => s.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected DB error"));

        var controller = CreateController(user: BuildUser(Guid.NewGuid(), "Admin_Staff"));

        // Act
        var result = await controller.SimulatePaymentAsStaff(
            paymentId,
            new SimulatePaymentRequest { Result = "Failed" },
            CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}
