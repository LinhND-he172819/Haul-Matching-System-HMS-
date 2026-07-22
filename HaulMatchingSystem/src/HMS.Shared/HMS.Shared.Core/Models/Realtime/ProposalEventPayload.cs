namespace HMS.Shared.Core.Models.Realtime
{
    /// <summary>
    /// Payload for proposal-specific SignalR events sent to drivers/customers.
    /// </summary>
    public class ProposalEventPayload
    {
        public string EventType { get; set; } = string.Empty; // NewShipmentProposal, ShipmentProposalCancelled, TripCapacityUpdated
        public Guid? ProposalId { get; set; }
        public Guid? TripPostId { get; set; }
        public int PendingProposalCount { get; set; }
        public decimal RemainingWeightKg { get; set; }
        public decimal RemainingVolumeCbm { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
