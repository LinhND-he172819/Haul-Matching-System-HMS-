using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
{
    public class CustomerStatusPayload
    {
        public Guid CustomerId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShipmentId { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationChannel Preference { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
