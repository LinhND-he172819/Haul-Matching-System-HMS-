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
        /// Get payment summary for a shipment.
        /// </summary>
        Task<PaymentSummaryDto> GetPaymentSummaryAsync(
            Guid shipmentId, CancellationToken ct);

        /// <summary>
        /// Get payment history for a quotation.
        /// </summary>
        Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(
            Guid quotationId, CancellationToken ct);

        /// <summary>
        /// Calculate outstanding amount for a shipment.
        /// </summary>
        Task<decimal> CalculateOutstandingAmountAsync(
            Guid shipmentId, CancellationToken ct);
    }
}
