namespace HMS.Modules.Matching.Application.DTOs
{
    /// <summary>
    /// Request to create a deposit payment for a quotation.
    /// </summary>
    public class CreateDepositPaymentRequest
    {
        public string? PaymentMethod { get; set; }
    }

    /// <summary>
    /// Request to create a final payment.
    /// </summary>
    public class CreateFinalPaymentRequest
    {
        public string? PaymentMethod { get; set; }
    }

    /// <summary>
    /// Response after creating a payment.
    /// </summary>
    public class PaymentResponseDto
    {
        public Guid Id { get; set; }
        public string PaymentCode { get; set; } = string.Empty;
        public Guid QuotationId { get; set; }
        public Guid ShipmentId { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? PaymentMethod { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool CanContinuePayment { get; set; }
        public bool CanCancel { get; set; }
    }

    /// <summary>
    /// Allowed actions for a payment based on its current status.
    /// </summary>
    public class PaymentAllowedActions
    {
        public bool CanContinuePayment { get; set; }
        public bool CanCancel { get; set; }
        public bool CanRetry { get; set; }
        public bool CanViewDetail { get; set; } = true;
    }

    /// <summary>
    /// Request to open mock checkout session.
    /// </summary>
    public class MockCheckoutRequest
    {
        public string PaymentMethod { get; set; } = "MockBanking";
    }

    /// <summary>
    /// Response from mock checkout session creation.
    /// </summary>
    public class MockCheckoutResponse
    {
        public Guid PaymentId { get; set; }
        public Guid CheckoutSessionId { get; set; }
        public string? CheckoutUrl { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Request to simulate payment result (dev/test only).
    /// </summary>
    public class SimulatePaymentRequest
    {
        public string Result { get; set; } = "Paid"; // "Paid", "Failed", "Cancelled"
    }

    /// <summary>
    /// Payment summary for shipment detail view.
    /// </summary>
    public class PaymentSummaryDto
    {
        public decimal DepositPaid { get; set; }
        public decimal FinalPaid { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public string? TransactionRef { get; set; }
        public string? PaymentStatus { get; set; }
    }

    /// <summary>
    /// Payment history entry.
    /// </summary>
    public class PaymentHistoryEntry
    {
        public Guid Id { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Webhook request for payment callback.
    /// </summary>
    public class PaymentWebhookRequest
    {
        public string TransactionReference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Paid" or "Failed"
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Signature { get; set; }
    }

    /// <summary>
    /// Full payment detail DTO for detail views.
    /// </summary>
    public class PaymentDetailDto
    {
        public Guid Id { get; set; }
        public string PaymentCode { get; set; } = string.Empty;
        public string? PaymentGateway { get; set; }
        public string? PaymentMethod { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? TransactionReference { get; set; }
        public string? FailureReason { get; set; }

        // Quotation info
        public Guid QuotationId { get; set; }
        public string? QuotationCode { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }

        // Shipment info
        public Guid ShipmentId { get; set; }
        public string? ShipmentCode { get; set; }
        public string? ShipmentStatus { get; set; }

        // Customer info
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }

        // Allowed actions
        public PaymentAllowedActions AllowedActions { get; set; } = new();

        // Timeline
        public List<PaymentTimelineEntry> Timeline { get; set; } = new();
    }

    /// <summary>
    /// Payment timeline entry built from audit log.
    /// </summary>
    public class PaymentTimelineEntry
    {
        public string Status { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Request to retry a failed payment.
    /// </summary>
    public class RetryPaymentRequest
    {
        public string? TransactionReference { get; set; }
    }

    /// <summary>
    /// Request to cancel a pending payment.
    /// </summary>
    public class CancelPaymentRequest
    {
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request to request a refund.
    /// </summary>
    public class RequestRefundRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
