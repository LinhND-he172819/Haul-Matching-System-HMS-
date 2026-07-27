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
}
