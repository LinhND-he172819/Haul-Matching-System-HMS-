namespace HMS.Shared.Core.Models.Realtime
{
    public class GpsPayload
    {
        public Guid TripId { get; set; }
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public double? Speed { get; set; }
        public DateTime DeviceTimestamp { get; set; }
    }
}
