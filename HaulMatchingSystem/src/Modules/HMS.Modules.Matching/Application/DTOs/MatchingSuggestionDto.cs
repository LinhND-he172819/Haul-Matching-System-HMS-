namespace HMS.Modules.Matching.Application.DTOs
{
    public class ShipmentSuggestionDto
    {
        public Guid ShipmentId { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? DestinationAddress { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeCbm { get; set; }
        public int DeliverySequence { get; set; }
        public string? SpecialHandlingNote { get; set; }
    }

    public class MatchingSuggestionsResponse
    {
        public Guid TripId { get; set; }
        public decimal CurrentLoadWeight { get; set; }
        public decimal CurrentLoadVolume { get; set; }
        public decimal RemainingWeightCapacity { get; set; }
        public decimal RemainingVolumeCapacity { get; set; }
        public List<ShipmentSuggestionDto> Shipments { get; set; } = new();
    }
}
