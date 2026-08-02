namespace HMS.Shared.Core.Models.Realtime
{
    public class IncidentReportedPayload
    {
        public Guid IncidentId { get; set; }
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public Guid DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public Guid? ShipmentId { get; set; }
        public string IncidentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
