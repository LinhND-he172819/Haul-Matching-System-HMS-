namespace HMS.Modules.Transport.Application.DTOs
{
    public class StaleTripQueryResult
    {
        public Guid TripId { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public DateTime? ServerReceivedAt { get; set; }
    }
}
