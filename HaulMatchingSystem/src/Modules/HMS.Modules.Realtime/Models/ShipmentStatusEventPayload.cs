using HMS.Shared.Core.Enums;

namespace HMS.Modules.Realtime.Models
{
    public class ShipmentStatusEventPayload
    {
        public Guid ShipmentId { get; set; }
        public string QrCode { get; set; } = string.Empty;
        public ShipmentStatus OldStatus { get; set; }
        public ShipmentStatus NewStatus { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
