using HMS.Shared.Core.Enums;

namespace HMS.Modules.Realtime.Models
{
    public class TripStatusEventPayload
    {
        public Guid TripId { get; set; }
        public Guid DriverId { get; set; }
        public TripStatus Status { get; set; }
        public DateTime UpdatedAt { get; set; }
    }   
}
