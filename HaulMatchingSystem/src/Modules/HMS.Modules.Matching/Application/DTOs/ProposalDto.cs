using HMS.Modules.Matching.Core.Models;

namespace HMS.Modules.Matching.Application.DTOs
{
    /// <summary>
    /// DTO for a single proposal shown to the Driver.
    /// </summary>
    public class ProposalDto
    {
        public Guid ProposalId { get; set; }
        public Guid ShipmentId { get; set; }
        public Guid TripPostId { get; set; }
        public string? ShipmentCode { get; set; }

        // â”€â”€ Shipment info (readonly from Shipment) â”€â”€
        public string? Commodity { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeCbm { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? DeliveryAddress { get; set; }

        // â”€â”€ Sender / Pickup info (from Proposal) â”€â”€
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public double? PickupLatitude { get; set; }
        public double? PickupLongitude { get; set; }
        public string? PickupNote { get; set; }

        // â”€â”€ Proposal meta â”€â”€
        public string Status { get; set; } = ProposalStatusConstants.Pending;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Response for driver's pending proposals list.
    /// </summary>
    public class DriverProposalsResponse
    {
        public Guid TripId { get; set; }
        public decimal CurrentLoadWeight { get; set; }
        public decimal CurrentLoadVolume { get; set; }
        public decimal RemainingWeightCapacity { get; set; }
        public decimal RemainingVolumeCapacity { get; set; }
        public List<ProposalDto> Proposals { get; set; } = new();
    }

    /// <summary>
    /// Request for creating a proposal (Customer side).
    /// </summary>
    public class CreateProposalRequest
    {
        public Guid ShipmentId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public double? PickupLatitude { get; set; }
        public double? PickupLongitude { get; set; }
        public string? PickupNote { get; set; }
    }

    /// <summary>
    /// Response after creating a proposal.
    /// </summary>
    public class CreateProposalResponse
    {
        public Guid ProposalId { get; set; }
        public Guid ShipmentId { get; set; }
        public Guid TripPostId { get; set; }
        public string Status { get; set; } = ProposalStatusConstants.Pending;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Request for rejecting a proposal (Driver side).
    /// </summary>
    public class RejectProposalRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for accepting all pending proposals.
    /// </summary>
    public class AcceptAllProposalsRequest
    {
        public Guid TripId { get; set; }
    }

    /// <summary>
    /// Summary of trip capacity for driver view.
    /// </summary>
    public class TripCapacityDto
    {
        public Guid TripId { get; set; }
        public decimal CurrentLoadWeight { get; set; }
        public decimal CurrentLoadVolume { get; set; }
        public decimal RemainingWeightCapacity { get; set; }
        public decimal RemainingVolumeCapacity { get; set; }
    }
}
