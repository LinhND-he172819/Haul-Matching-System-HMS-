using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models.Realtime
{
    public class AnomalyAlertPayload
    {
        public Guid TripId { get; set; }
        public Guid VehicleId { get; set; }
        public ExceptionType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
