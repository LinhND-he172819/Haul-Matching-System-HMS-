namespace HMS.Shared.Core.Models.Realtime
{
    public class MatchingNotificationPayload
    {
        public Guid DriverId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public int ShipmentCount { get; set; }
        public decimal TotalWeightKg { get; set; }
        public string TopDeliveryAddress { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
