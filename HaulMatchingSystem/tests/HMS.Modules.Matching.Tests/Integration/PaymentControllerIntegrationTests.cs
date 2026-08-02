using System.Security.Claims;
using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Controllers;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HMS.Modules.Matching.Tests.Integration
{
    /// <summary>
    /// Controller-level integration tests for the new Payment endpoints (Parts 1, 3, 5, 6, 7, 8).
    /// Tests verify that controllers correctly route to service methods, handle exceptions,
    /// and return appropriate HTTP status codes.
    /// </summary>
    public class PaymentControllerIntegrationTests
    {
        private readonly Mock<IPaymentService> _paymentService;
        private readonly Mock<IQuotationService> _quotationService;
        private readonly Mock<ILogger<CustomerQuotationController>> _customerLogger;
        private readonly Mock<ILogger<StaffPaymentController>> _staffLogger;
        private readonly Mock<ILogger<DriverCODController>> _driverLogger;

        private readonly Guid _customerId = Guid.NewGuid();
        private readonly Guid _staffId = Guid.NewGuid();
        private readonly Guid _driverId = Guid.NewGuid();
        private readonly Guid _paymentId = Guid.NewGuid();

        public PaymentControllerIntegrationTests()
        {
            _paymentService = new Mock<IPaymentService>();
            _quotationService = new Mock<IQuotationService>();
            _customerLogger = new Mock<ILogger<CustomerQuotationController>>();
            _staffLogger = new Mock<ILogger<StaffPaymentController>>();
            _driverLogger = new Mock<ILogger<DriverCODController>>();
        }

        private CustomerQuotationController CreateCustomerController(Guid? userId = null)
        {
            var controller = new CustomerQuotationController(
                _quotationService.Object, _paymentService.Object, _customerLogger.Object);
            SetCurrentUser(controller, userId ?? _customerId, "Customer");
            return controller;
        }

        private StaffPaymentController CreateStaffController(Guid? userId = null, string role = "Admin", Guid? hubId = null)
        {
            var staffProposalService = new Mock<IStaffProposalService>();
            var controller = new StaffPaymentController(
                staffProposalService.Object, _paymentService.Object, _staffLogger.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, (userId ?? _staffId).ToString()),
                new Claim("sub", (userId ?? _staffId).ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            if (hubId.HasValue)
                claims.Add(new Claim("HubId", hubId.Value.ToString()));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };
            return controller;
        }

        private DriverCODController CreateDriverController(Guid? userId = null)
        {
            var controller = new DriverCODController(_paymentService.Object, _driverLogger.Object);
            SetCurrentUser(controller, userId ?? _driverId, "Driver");
            return controller;
        }

        private void SetCurrentUser(ControllerBase controller, Guid userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };
        }

        // ═══ Part 1: Retry Payment (Customer) ═══

        [Fact]
        public async Task CustomerRetryPayment_Success_ReturnsOk()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.RetryPaymentAsync(_paymentId, _customerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponseDto { Id = _paymentId, Status = "Pending" });

            var result = await controller.RetryPayment(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResponseDto>(okResult.Value);
            Assert.Equal("Pending", paymentResult.Status);
        }

        [Fact]
        public async Task CustomerRetryPayment_WrongStatus_ReturnsBadRequest()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.RetryPaymentAsync(_paymentId, _customerId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể thử lại thanh toán ở trạng thái Failed."));

            var result = await controller.RetryPayment(_paymentId, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CustomerRetryPayment_Forbidden_ReturnsForbid()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.RetryPaymentAsync(_paymentId, _customerId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());

            var result = await controller.RetryPayment(_paymentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        }

        // ═══ Part 3: Cancel Payment (Customer) ═══

        [Fact]
        public async Task CustomerCancelPayment_Success_ReturnsOk()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.CancelPaymentAsync(_paymentId, _customerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponseDto { Id = _paymentId, Status = "Cancelled" });

            var result = await controller.CancelPayment(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResponseDto>(okResult.Value);
            Assert.Equal("Cancelled", paymentResult.Status);
        }

        [Fact]
        public async Task CustomerCancelPayment_WrongStatus_ReturnsBadRequest()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.CancelPaymentAsync(_paymentId, _customerId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể hủy thanh toán ở trạng thái Pending."));

            var result = await controller.CancelPayment(_paymentId, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ═══ Part 5: Payment Detail (Customer + Staff) ═══

        [Fact]
        public async Task CustomerGetPaymentDetail_Success_ReturnsOk()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.GetPaymentDetailAsync(
                _paymentId, _customerId, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentDetailDto { Id = _paymentId, PaymentCode = "PAY-001" });

            var result = await controller.GetPaymentDetail(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task CustomerGetPaymentDetail_NotFound_ReturnsNotFound()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.GetPaymentDetailAsync(
                _paymentId, _customerId, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentDetailDto?)null);

            var result = await controller.GetPaymentDetail(_paymentId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CustomerGetPaymentDetail_Forbidden_ReturnsForbid()
        {
            var controller = CreateCustomerController();
            _paymentService.Setup(s => s.GetPaymentDetailAsync(
                _paymentId, _customerId, null, null, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Access denied"));

            var result = await controller.GetPaymentDetail(_paymentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task StaffGetPaymentDetail_Success_ReturnsOk()
        {
            var controller = CreateStaffController(role: "Admin");
            _paymentService.Setup(s => s.GetStaffPaymentDetailAsync(
                _paymentId, _staffId, "Admin", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentDetailDto { Id = _paymentId, PaymentCode = "PAY-001" });

            var result = await controller.GetPaymentDetail(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task StaffGetPaymentDetail_Forbidden_ReturnsForbid()
        {
            var controller = CreateStaffController(role: "Warehouse_Staff", hubId: Guid.NewGuid());
            _paymentService.Setup(s => s.GetStaffPaymentDetailAsync(
                _paymentId, _staffId, "Warehouse_Staff", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Access denied"));

            var result = await controller.GetPaymentDetail(_paymentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        }

        // ═══ Part 6: Payment Timeline ═══

        [Fact]
        public async Task CustomerGetPaymentTimeline_Success_ReturnsOk()
        {
            var controller = CreateCustomerController();
            var timeline = new List<PaymentTimelineEntry>
            {
                new PaymentTimelineEntry { Status = "Pending", Action = "Created", OccurredAt = DateTime.UtcNow }
            };
            _paymentService.Setup(s => s.GetPaymentTimelineAsync(_paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(timeline);

            var result = await controller.GetPaymentTimeline(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var entries = Assert.IsAssignableFrom<List<PaymentTimelineEntry>>(okResult.Value);
            Assert.Single(entries);
        }

        [Fact]
        public async Task StaffGetPaymentTimeline_Success_ReturnsOk()
        {
            var controller = CreateStaffController();
            _paymentService.Setup(s => s.GetPaymentTimelineAsync(_paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PaymentTimelineEntry>());

            var result = await controller.GetPaymentTimeline(_paymentId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }

        // ═══ Part 7: Refund (Staff) ═══

        [Fact]
        public async Task StaffRequestRefund_Success_ReturnsOk()
        {
            var controller = CreateStaffController();
            _paymentService.Setup(s => s.RequestRefundAsync(_paymentId, _staffId, "Customer complaint", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponseDto { Id = _paymentId, Status = "PendingRefund" });

            var result = await controller.RequestRefund(
                _paymentId, new RequestRefundRequest { Reason = "Customer complaint" }, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResponseDto>(okResult.Value);
            Assert.Equal("PendingRefund", paymentResult.Status);
        }

        [Fact]
        public async Task StaffRequestRefund_WrongStatus_ReturnsBadRequest()
        {
            var controller = CreateStaffController();
            _paymentService.Setup(s => s.RequestRefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể yêu cầu hoàn tiền cho thanh toán ở trạng thái Paid."));

            var result = await controller.RequestRefund(
                _paymentId, new RequestRefundRequest { Reason = "reason" }, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task StaffApproveRefund_Success_ReturnsOk()
        {
            var controller = CreateStaffController(role: "Admin");
            _paymentService.Setup(s => s.ApproveRefundAsync(_paymentId, _staffId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponseDto { Id = _paymentId, Status = "Refunded" });

            var result = await controller.ApproveRefund(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResponseDto>(okResult.Value);
            Assert.Equal("Refunded", paymentResult.Status);
        }

        [Fact]
        public async Task StaffApproveRefund_NonAdmin_ReturnsForbid()
        {
            var controller = CreateStaffController(role: "Warehouse_Staff");
            _paymentService.Setup(s => s.ApproveRefundAsync(_paymentId, _staffId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Chỉ quản trị viên mới có quyền duyệt hoàn tiền."));

            var result = await controller.ApproveRefund(_paymentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        }

        // ═══ Part 8: COD Confirmation (Driver) ═══

        [Fact]
        public async Task DriverConfirmCodPayment_Success_ReturnsOk()
        {
            var controller = CreateDriverController();
            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(_paymentId, _driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResponseDto { Id = _paymentId, Status = "Paid", ShipmentId = Guid.NewGuid() });

            var result = await controller.ConfirmCodPayment(_paymentId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResponseDto>(okResult.Value);
            Assert.Equal("Paid", paymentResult.Status);
            Assert.NotEqual(Guid.Empty, paymentResult.ShipmentId);
        }

        [Fact]
        public async Task DriverConfirmCodPayment_WrongStatus_ReturnsBadRequest()
        {
            var controller = CreateDriverController();
            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(_paymentId, _driverId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể xác nhận COD cho thanh toán Pending."));

            var result = await controller.ConfirmCodPayment(_paymentId, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DriverConfirmCodPayment_Forbidden_ReturnsForbid()
        {
            var controller = CreateDriverController();
            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(_paymentId, _driverId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Chỉ tài xế mới có quyền xác nhận thanh toán COD."));

            var result = await controller.ConfirmCodPayment(_paymentId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        }
    }
}
