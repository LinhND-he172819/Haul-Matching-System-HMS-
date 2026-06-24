namespace HMS.Shared.Core.Models.Realtime
{
    public class AdminStatsPayload
    {
        public int ActiveTripCount { get; set; }
        public int InTransitShipments { get; set; }
        public double AvgVehicleUtilisation { get; set; }
        public int HubItemsWaitingOver3Days { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
