using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
{
    public class TripStatusEventPayload
    {
        public Guid TripId { get; set; }
        public Guid DriverId { get; set; }
        public TripStatus Status { get; set; }
        public DateTime UpdatedAt { get; set; }
    }   
}
