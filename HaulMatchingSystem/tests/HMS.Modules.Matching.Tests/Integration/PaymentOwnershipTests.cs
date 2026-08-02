using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Controllers;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HMS.Modules.Matching.Tests.Integration
{
    /// <summary>
    /// Controller-level integration tests verifying that payment endpoints
    /// enforce Customer ownership: Customer A cannot read payment data belonging
    /// to Customer B. These tests exercise the full controller → service
    /// authorization boundary with mocked IPaymentService.
    /// </summary>
    public class PaymentOwnershipTests
    {
        private readonly Mock<IPaymentService> _paymentService;
        private readonly Mock<IQuotationService> _quotationService;
        private readonly Mock<ILogger<CustomerQuotationController>> _logger;
        private readonly CustomerQuotationController _controller;

        private readonly Guid _customerAId = Guid.NewGuid();
        private readonly Guid _customerBId = Guid.NewGuid();
        private readonly Guid _shipmentId = Guid.NewGuid();
        private readonly Guid _quotationId = Guid.NewGuid();

        public PaymentOwnershipTests()
        {
            _paymentService = new Mock<IPaymentService>();
            _quotationService = new Mock<IQuotationService>();
            _logger = new Mock<ILogger<CustomerQuotationController>>();

            _controller = new CustomerQuotationController(
                _quotationService.Object,
                _paymentService.Object,
                _logger.Object);
        }

        /// <summary>
        /// Configure the controller's HttpContext to simulate an authenticated user.
        /// </summary>
        private void SetCurrentUser(Guid userId, string role = "Customer")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  PAYMENT SUMMARY — Ownership verification
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetPaymentSummary_CustomerA_OwnShipment_ReturnsData()
        {
            SetCurrentUser(_customerAId);

            var expected = new PaymentSummaryDto
            {
                DepositPaid = 100, FinalPaid = 200, TotalPaid = 300,
                OutstandingAmount = 50, ShippingFee = 350, DepositAmount = 100
            };

            _paymentService.Setup(s => s.GetPaymentSummaryAsync(
                    _shipmentId, _customerAId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await _controller.GetPaymentSummary(_shipmentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PaymentSummaryDto>(okResult.Value);
            Assert.Equal(300, dto.TotalPaid);

            // Verify customerId from JWT was passed to service
            _paymentService.Verify(s => s.GetPaymentSummaryAsync(
                _shipmentId, _customerAId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPaymentSummary_CustomerB_OtherShipment_ThrowsForbidden()
        {
            SetCurrentUser(_customerBId);

            _paymentService.Setup(s => s.GetPaymentSummaryAsync(
                    _shipmentId, _customerBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Không có quyền xem thông tin thanh toán cho Shipment này."));

            var result = await _controller.GetPaymentSummary(_shipmentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);

            // Verify customerId from JWT was passed to service
            _paymentService.Verify(s => s.GetPaymentSummaryAsync(
                _shipmentId, _customerBId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PAYMENT HISTORY — Ownership verification
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetPaymentHistory_CustomerA_OwnQuotation_ReturnsData()
        {
            SetCurrentUser(_customerAId);

            var expected = new List<PaymentHistoryEntry>
            {
                new() { Id = Guid.NewGuid(), PaymentType = "Deposit", Amount = 100, Status = "Paid" },
                new() { Id = Guid.NewGuid(), PaymentType = "FinalPayment", Amount = 250, Status = "Pending" }
            };

            _paymentService.Setup(s => s.GetPaymentHistoryAsync(
                    _quotationId, _customerAId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await _controller.GetPaymentHistory(_quotationId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsType<List<PaymentHistoryEntry>>(okResult.Value);
            Assert.Equal(2, list.Count);

            // Verify customerId from JWT was passed to service
            _paymentService.Verify(s => s.GetPaymentHistoryAsync(
                _quotationId, _customerAId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPaymentHistory_CustomerB_OtherQuotation_ThrowsForbidden()
        {
            SetCurrentUser(_customerBId);

            _paymentService.Setup(s => s.GetPaymentHistoryAsync(
                    _quotationId, _customerBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Không có quyền xem lịch sử thanh toán cho Báo giá này."));

            var result = await _controller.GetPaymentHistory(_quotationId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);

            // Verify customerId from JWT was passed to service
            _paymentService.Verify(s => s.GetPaymentHistoryAsync(
                _quotationId, _customerBId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Cross-customer isolation — Customer A's JWT is always used
        //  to verify that different customer IDs produce different results
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetPaymentSummary_DifferentCustomers_GetDifferentIds()
        {
            // Customer A accesses their own shipment
            SetCurrentUser(_customerAId);
            _paymentService.Setup(s => s.GetPaymentSummaryAsync(
                    _shipmentId, _customerAId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentSummaryDto { TotalPaid = 300 });

            var resultA = await _controller.GetPaymentSummary(_shipmentId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(resultA);

            // Customer B accesses the SAME shipment — ownership check fails
            SetCurrentUser(_customerBId);
            _paymentService.Setup(s => s.GetPaymentSummaryAsync(
                    _shipmentId, _customerBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Không có quyền."));

            var resultB = await _controller.GetPaymentSummary(_shipmentId, CancellationToken.None);
            Assert.IsType<ForbidResult>(resultB);

            // Verify service was called twice with different customer IDs
            _paymentService.Verify(s => s.GetPaymentSummaryAsync(
                _shipmentId, _customerAId, It.IsAny<CancellationToken>()), Times.Once);
            _paymentService.Verify(s => s.GetPaymentSummaryAsync(
                _shipmentId, _customerBId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPaymentHistory_DifferentCustomers_GetDifferentIds()
        {
            // Customer A accesses their own quotation
            SetCurrentUser(_customerAId);
            _paymentService.Setup(s => s.GetPaymentHistoryAsync(
                    _quotationId, _customerAId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PaymentHistoryEntry>
                {
                    new() { Id = Guid.NewGuid(), PaymentType = "Deposit", Amount = 100, Status = "Paid" }
                });

            var resultA = await _controller.GetPaymentHistory(_quotationId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(resultA);

            // Customer B accesses the SAME quotation — ownership check fails
            SetCurrentUser(_customerBId);
            _paymentService.Setup(s => s.GetPaymentHistoryAsync(
                    _quotationId, _customerBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Không có quyền."));

            var resultB = await _controller.GetPaymentHistory(_quotationId, CancellationToken.None);
            Assert.IsType<ForbidResult>(resultB);

            // Verify service was called twice with different customer IDs
            _paymentService.Verify(s => s.GetPaymentHistoryAsync(
                _quotationId, _customerAId, It.IsAny<CancellationToken>()), Times.Once);
            _paymentService.Verify(s => s.GetPaymentHistoryAsync(
                _quotationId, _customerBId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Missing JWT claim → service receives no customerId
        //  (should not happen in production due to [Authorize], but
        //   verifies the defensive path)
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetPaymentSummary_NoUserIdClaim_Returns500()
        {
            // Set up a principal with NO NameIdentifier claim
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // GetCurrentUserId throws UnauthorizedAccessException, caught → Forbid()
            var result = await _controller.GetPaymentSummary(_shipmentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);

            // Service should NOT be called
            _paymentService.Verify(s => s.GetPaymentSummaryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetPaymentHistory_NoUserIdClaim_Returns500()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await _controller.GetPaymentHistory(_quotationId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);

            _paymentService.Verify(s => s.GetPaymentHistoryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
