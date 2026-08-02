using HMS.Modules.Matching.Application.DTOs;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Service for managing payments.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Create a deposit payment for a quotation.
        /// </summary>
        Task<PaymentResponseDto> CreateDepositPaymentAsync(
            Guid quotationId, Guid customerId, CreateDepositPaymentRequest request,
            CancellationToken ct);

        /// <summary>
        /// Create a final payment for a quotation.
        /// </summary>
        Task<PaymentResponseDto> CreateFinalPaymentAsync(
            Guid quotationId, Guid customerId, CreateFinalPaymentRequest request,
            CancellationToken ct);

        /// <summary>
        /// Process a payment webhook/callback.
        /// Handles: deposit acceptance, capacity update, shipment matching.
        /// </summary>
        Task ProcessWebhookAsync(
            PaymentWebhookRequest webhook, CancellationToken ct);

        /// <summary>
        /// Get payment summary for a shipment (customer ownership verified).
        /// </summary>
        Task<PaymentSummaryDto> GetPaymentSummaryAsync(
            Guid shipmentId, Guid? customerId, CancellationToken ct);

        /// <summary>
        /// Get payment history for a quotation (customer ownership verified).
        /// </summary>
        Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(
            Guid quotationId, Guid? customerId, CancellationToken ct);

        /// <summary>
        /// Calculate outstanding amount for a shipment.
        /// </summary>
        Task<decimal> CalculateOutstandingAmountAsync(
            Guid shipmentId, Guid? customerId, CancellationToken ct);

        /// <summary>
        /// Retry a failed payment: Failed → Pending.
        /// Keeps payment_code, updates status/transaction_reference/updated_at.
        /// </summary>
        Task<PaymentResponseDto> RetryPaymentAsync(
            Guid paymentId, Guid customerId, CancellationToken ct);

        /// <summary>
        /// Cancel a pending payment: Pending → Cancelled.
        /// </summary>
        Task<PaymentResponseDto> CancelPaymentAsync(
            Guid paymentId, Guid customerId, CancellationToken ct);

        /// <summary>
        /// Get full payment detail by ID.
        /// </summary>
        Task<PaymentDetailDto?> GetPaymentDetailAsync(
            Guid paymentId, Guid? customerId, Guid? staffId, string? role, Guid? hubId,
            CancellationToken ct);

        /// <summary>
        /// Get payment timeline (status history from audit log).
        /// </summary>
        Task<List<PaymentTimelineEntry>> GetPaymentTimelineAsync(
            Guid paymentId, CancellationToken ct);

        /// <summary>
        /// Request a refund: Paid → PendingRefund.
        /// </summary>
        Task<PaymentResponseDto> RequestRefundAsync(
            Guid paymentId, Guid staffId, string reason, CancellationToken ct);

        /// <summary>
        /// Approve/Complete a refund: PendingRefund → Refunded.
        /// </summary>
        Task<PaymentResponseDto> ApproveRefundAsync(
            Guid paymentId, Guid staffId, CancellationToken ct);

        /// <summary>
        /// Confirm COD payment: Delivered → Paid → Completed (no webhook).
        /// </summary>
        Task<PaymentResponseDto> ConfirmCodPaymentAsync(
            Guid paymentId, Guid driverId, CancellationToken ct);

        /// <summary>
        /// Staff get payment detail by ID (with hub scoping for Warehouse_Staff).
        /// </summary>
        Task<PaymentDetailDto?> GetStaffPaymentDetailAsync(
            Guid paymentId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct);
    }
}
