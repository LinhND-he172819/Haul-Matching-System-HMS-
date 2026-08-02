using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
{
    public class DriverTripEventPayload
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public Guid DriverId { get; set; }
        public TripStatus Status { get; set; }
        public string EventType { get; set; } = string.Empty; // "TripStarted", "TripCompleted", "IncidentReported"
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
