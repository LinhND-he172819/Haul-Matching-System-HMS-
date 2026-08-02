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
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
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
