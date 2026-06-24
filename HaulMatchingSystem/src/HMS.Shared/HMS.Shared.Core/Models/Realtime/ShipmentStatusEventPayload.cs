using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
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
