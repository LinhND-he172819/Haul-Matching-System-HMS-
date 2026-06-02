namespace HMS.Modules.Realtime.Models
{
    public class GpsPayload
    {
        public Guid TripId { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double? Speed { get; set; }
        public DateTime DeviceTimestamp { get; set; }
    }
}
