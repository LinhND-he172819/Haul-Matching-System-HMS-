using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
{
    public class ShipmentActionPayload
    {
        public Guid ShipmentId { get; set; }
        public string ShipmentCode { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public Guid? DriverId { get; set; }
        public ShipmentStatus OldStatus { get; set; }
        public ShipmentStatus NewStatus { get; set; }
        public string EventType { get; set; } = string.Empty; // "PickupConfirmed", "TransportStarted", "DeliveryConfirmed", "DraftUpdated", "ShipmentCancelled"
        public string? Note { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
