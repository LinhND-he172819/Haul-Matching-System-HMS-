namespace HMS.Shared.Core.Models.Realtime
{
    /// <summary>
    /// Payload for payment-specific SignalR events sent to customers/staff.
    /// </summary>
    public class PaymentEventPayload
    {
        public string EventType { get; set; } = string.Empty; // DepositPaid, FinalPaymentPaid, PaymentFailed
        public Guid PaymentId { get; set; }
        public Guid ShipmentId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
