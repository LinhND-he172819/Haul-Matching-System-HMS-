using HMS.Shared.Core.Enums;

namespace HMS.Modules.Realtime.Models
{
    public class AnomalyAlertPayload
    {
        public Guid TripId { get; set; }
        public ExceptionType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
