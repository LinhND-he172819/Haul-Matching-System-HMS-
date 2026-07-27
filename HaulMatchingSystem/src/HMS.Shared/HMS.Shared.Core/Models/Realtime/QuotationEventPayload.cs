namespace HMS.Shared.Core.Models.Realtime
{
    /// <summary>
    /// Payload for quotation-specific SignalR events sent to customers.
    /// </summary>
    public class QuotationEventPayload
    {
        public string EventType { get; set; } = string.Empty; // QuotationSent, QuotationCancelled, QuotationExpired
        public Guid QuotationId { get; set; }
        public Guid ProposalId { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
