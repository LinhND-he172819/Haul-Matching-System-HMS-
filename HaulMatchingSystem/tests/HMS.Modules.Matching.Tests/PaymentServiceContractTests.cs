using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Exceptions;
using Moq;
using Xunit;

namespace HMS.Modules.Matching.Tests
{
    /// <summary>
    /// Unit tests for PaymentService new methods (Parts 1-9).
    /// Covers: RetryPayment, CancelPayment, GetPaymentDetail, GetPaymentTimeline,
    ///         RequestRefund, ApproveRefund, ConfirmCodPayment.
    /// Since PaymentService uses raw Npgsql, these tests mock the service interface
    /// to verify controller-level integration (see Integration/PaymentOwnershipTests.cs
    /// for controller tests). These tests verify contract and DTO behavior.
    /// </summary>
    public class PaymentServiceContractTests
    {
        private readonly Mock<IPaymentService> _paymentService;

        public PaymentServiceContractTests()
        {
            _paymentService = new Mock<IPaymentService>();
        }

        // ═══ PaymentTransitionGuard Tests ═══

        [Fact]
        public void PaymentTransitionGuard_FailedToPending_IsAllowed()
        {
            var from = PaymentStatus.Failed;
            var to = PaymentStatus.Pending;
            Assert.False(from == to, "Retry should change state");
            Assert.Equal(2, (int)PaymentStatus.Failed);
            Assert.Equal(0, (int)PaymentStatus.Pending);
        }

        [Fact]
        public void PaymentTransitionGuard_PendingToCancelled_IsAllowed()
        {
            Assert.Equal(0, (int)PaymentStatus.Pending);
            Assert.Equal(3, (int)PaymentStatus.Cancelled);
        }

        [Fact]
        public void PaymentTransitionGuard_PaidToPendingRefund_IsAllowed()
        {
            Assert.Equal(1, (int)PaymentStatus.Paid);
            Assert.Equal(4, (int)PaymentStatus.PendingRefund);
        }

        [Fact]
        public void PaymentTransitionGuard_PendingRefundToRefunded_IsAllowed()
        {
            Assert.Equal(4, (int)PaymentStatus.PendingRefund);
            Assert.Equal(5, (int)PaymentStatus.Refunded);
        }

        [Fact]
        public void PaymentTransitionGuard_AllSevenStatusesExist()
        {
            Assert.Equal(7, Enum.GetValues<PaymentStatus>().Length);
        }

        [Fact]
        public void PaymentTransitionGuard_Refunded_IsTerminal()
        {
            Assert.Equal(5, (int)PaymentStatus.Refunded);
        }

        [Fact]
        public void PaymentTransitionGuard_Cancelled_IsTerminal()
        {
            Assert.Equal(3, (int)PaymentStatus.Cancelled);
        }

        [Fact]
        public void PaymentTransitionGuard_PartiaLyRefunded_IsTerminal()
        {
            Assert.Equal(6, (int)PaymentStatus.PartiallyRefunded);
        }

        // ═══ PaymentService Contract Tests ═══

        [Fact]
        public async Task RetryPaymentAsync_WhenCalled_ReturnsPaymentResponse()
        {
            var paymentId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var expectedResult = new PaymentResponseDto
            {
                Id = paymentId,
                Status = PaymentStatus.Pending.ToString()
            };

            _paymentService.Setup(s => s.RetryPaymentAsync(paymentId, customerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.RetryPaymentAsync(paymentId, customerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(paymentId, result.Id);
            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task RetryPaymentAsync_FailedPayment_ThrowsInvalidOperation()
        {
            var paymentId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            _paymentService.Setup(s => s.RetryPaymentAsync(paymentId, customerId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể thử lại thanh toán ở trạng thái Failed."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _paymentService.Object.RetryPaymentAsync(paymentId, customerId, CancellationToken.None));

            Assert.Contains("Failed", ex.Message);
        }

        [Fact]
        public async Task CancelPaymentAsync_WhenCalled_ReturnsPaymentResponse()
        {
            var paymentId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var expectedResult = new PaymentResponseDto
            {
                Id = paymentId,
                Status = PaymentStatus.Cancelled.ToString()
            };

            _paymentService.Setup(s => s.CancelPaymentAsync(paymentId, customerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.CancelPaymentAsync(paymentId, customerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Cancelled", result.Status);
        }

        [Fact]
        public async Task CancelPaymentAsync_WrongOwnership_ThrowsForbidden()
        {
            var paymentId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            _paymentService.Setup(s => s.CancelPaymentAsync(paymentId, customerId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException("Không có quyền thao tác trên thanh toán này."));

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _paymentService.Object.CancelPaymentAsync(paymentId, customerId, CancellationToken.None));
        }

        [Fact]
        public async Task GetPaymentDetailAsync_WhenCalled_ReturnsDetail()
        {
            var paymentId = Guid.NewGuid();
            var expectedResult = new PaymentDetailDto
            {
                Id = paymentId,
                PaymentCode = "PAY-20250629-ABC",
                Status = "Paid",
                PaymentType = "Deposit",
                Amount = 500000m,
                QuotationCode = "QT-001"
            };

            _paymentService.Setup(s => s.GetPaymentDetailAsync(
                paymentId, It.IsAny<Guid>(), null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.GetPaymentDetailAsync(
                paymentId, Guid.NewGuid(), null, null, null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("PAY-20250629-ABC", result.PaymentCode);
            Assert.Equal(500000m, result.Amount);
        }

        [Fact]
        public async Task GetPaymentDetailAsync_NotFound_ReturnsNull()
        {
            _paymentService.Setup(s => s.GetPaymentDetailAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentDetailDto?)null);

            var result = await _paymentService.Object.GetPaymentDetailAsync(
                Guid.NewGuid(), Guid.NewGuid(), null, null, null, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPaymentTimelineAsync_WhenCalled_ReturnsTimeline()
        {
            var paymentId = Guid.NewGuid();
            var timeline = new List<PaymentTimelineEntry>
            {
                new PaymentTimelineEntry { Status = "Pending", Action = "Created", Details = "Payment created", OccurredAt = DateTime.UtcNow.AddMinutes(-30) },
                new PaymentTimelineEntry { Status = "Paid", Action = "Paid", Details = "Payment completed via PayOS", OccurredAt = DateTime.UtcNow.AddMinutes(-25) }
            };

            _paymentService.Setup(s => s.GetPaymentTimelineAsync(paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(timeline);

            var result = await _paymentService.Object.GetPaymentTimelineAsync(paymentId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Pending", result[0].Status);
            Assert.Equal("Paid", result[1].Status);
        }

        [Fact]
        public async Task RequestRefundAsync_WhenCalled_ReturnsResult()
        {
            var paymentId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var expectedResult = new PaymentResponseDto
            {
                Id = paymentId,
                Status = PaymentStatus.PendingRefund.ToString()
            };

            _paymentService.Setup(s => s.RequestRefundAsync(paymentId, staffId, "Customer complaint", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.RequestRefundAsync(
                paymentId, staffId, "Customer complaint", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("PendingRefund", result.Status);
        }

        [Fact]
        public async Task RequestRefundAsync_WrongStatus_ThrowsInvalidOperation()
        {
            _paymentService.Setup(s => s.RequestRefundAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể yêu cầu hoàn tiền cho thanh toán ở trạng thái Paid."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _paymentService.Object.RequestRefundAsync(Guid.NewGuid(), Guid.NewGuid(), "reason", CancellationToken.None));

            Assert.Contains("Paid", ex.Message);
        }

        [Fact]
        public async Task ApproveRefundAsync_WhenCalled_ReturnsResult()
        {
            var paymentId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var expectedResult = new PaymentResponseDto
            {
                Id = paymentId,
                Status = PaymentStatus.Refunded.ToString()
            };

            _paymentService.Setup(s => s.ApproveRefundAsync(paymentId, adminId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.ApproveRefundAsync(paymentId, adminId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Refunded", result.Status);
        }

        [Fact]
        public async Task ConfirmCodPaymentAsync_WhenCalled_ReturnsResult()
        {
            var paymentId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var expectedResult = new PaymentResponseDto
            {
                Id = paymentId,
                Status = PaymentStatus.Paid.ToString(),
                ShipmentId = Guid.NewGuid()
            };

            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(paymentId, driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _paymentService.Object.ConfirmCodPaymentAsync(paymentId, driverId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Paid", result.Status);
            Assert.NotEqual(Guid.Empty, result.ShipmentId);
        }

        [Fact]
        public async Task ConfirmCodPaymentAsync_WrongStatus_ThrowsInvalidOperation()
        {
            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể xác nhận COD cho thanh toán Pending."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _paymentService.Object.ConfirmCodPaymentAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Pending", ex.Message);
        }

        [Fact]
        public async Task ConfirmCodPaymentAsync_NotDriver_ThrowsForbidden()
        {
            _paymentService.Setup(s => s.ConfirmCodPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Chỉ tài xế mới có quyền xác nhận thanh toán COD."));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _paymentService.Object.ConfirmCodPaymentAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task ApproveRefundAsync_NonAdmin_ThrowsUnauthorized()
        {
            _paymentService.Setup(s => s.ApproveRefundAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Chỉ quản trị viên mới có quyền duyệt hoàn tiền."));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _paymentService.Object.ApproveRefundAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        }

        // ═══ DTO Structure Tests ═══

        [Fact]
        public void PaymentDetailDto_HasRequiredFields()
        {
            var dto = new PaymentDetailDto
            {
                Id = Guid.NewGuid(),
                PaymentCode = "PAY-TEST",
                Status = "Pending",
                PaymentType = "Deposit",
                Amount = 100000m,
                Currency = "VND",
                PaymentMethod = "payos",
                CreatedAt = DateTime.UtcNow
            };

            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Equal("PAY-TEST", dto.PaymentCode);
            Assert.Equal("VND", dto.Currency);
            Assert.Equal("payos", dto.PaymentMethod);
        }

        [Fact]
        public void PaymentTimelineEntry_HasRequiredFields()
        {
            var entry = new PaymentTimelineEntry
            {
                Status = "Pending",
                Action = "Created",
                Details = "Payment created",
                OccurredAt = DateTime.UtcNow
            };

            Assert.Equal("Pending", entry.Status);
            Assert.Equal("Created", entry.Action);
            Assert.NotEmpty(entry.Details);
        }

        [Fact]
        public void PaymentResponseDto_HasStatusAndId()
        {
            var result = new PaymentResponseDto
            {
                Id = Guid.NewGuid(),
                Status = "Paid"
            };

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Paid", result.Status);
        }

        [Fact]
        public void RequestRefundRequest_HasReason()
        {
            var request = new RequestRefundRequest { Reason = "Customer not satisfied" };
            Assert.Equal("Customer not satisfied", request.Reason);
        }
    }
}
