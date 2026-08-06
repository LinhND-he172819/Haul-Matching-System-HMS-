namespace HMS.Modules.Matching.Application.DTOs
{
    /// <summary>
    /// Request for a Driver to declare an external shipment.
    /// The trip is automatically determined from the driver's active trip.
    /// </summary>
    public sealed record CreateExternalShipmentRequest
    {
        // Sender
        public string SenderName { get; init; } = string.Empty;
        public string SenderPhone { get; init; } = string.Empty;
        public string PickupAddress { get; init; } = string.Empty;

        // Receiver
        public string ReceiverName { get; init; } = string.Empty;
        public string ReceiverPhone { get; init; } = string.Empty;
        public string DestAddress { get; init; } = string.Empty;

        // Shipment
        public string Category { get; init; } = string.Empty;
        public string? Description { get; init; }
        public decimal WeightKg { get; init; }
        public decimal VolumeCbm { get; init; }
        public int Quantity { get; init; } = 1;
        public bool CodRequired { get; init; }
        public string? Note { get; init; }
    }

    /// <summary>
    /// Response after creating an external shipment.
    /// </summary>
    public sealed record CreateExternalShipmentResponse
    {
        public Guid ShipmentId { get; init; }
        public Guid ProposalId { get; init; }
        public Guid RequestedTripId { get; init; }
        public string ShipmentCode { get; init; } = string.Empty;
        public string ProposalStatus { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    /// <summary>
    /// Summary of a driver-declared external shipment (for history list).
    /// </summary>
    public sealed record ExternalShipmentListItem
    {
        public Guid ShipmentId { get; init; }
        public Guid ProposalId { get; init; }
        public string? ShipmentCode { get; init; }
        public string? ProposalCode { get; init; }
        public string Category { get; init; } = string.Empty;
        public decimal WeightKg { get; init; }
        public decimal VolumeCbm { get; init; }
        public int Quantity { get; init; }
        public string ReceiverName { get; init; } = string.Empty;
        public string? DestAddress { get; init; }
        public string ProposalStatus { get; init; } = string.Empty;
        public string ShipmentStatus { get; init; } = string.Empty;
        public string? QuotationStatus { get; init; }
        public string? PaymentStatus { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    /// <summary>
    /// Detail view for a driver-declared external shipment.
    /// </summary>
    public sealed record ExternalShipmentDetail
    {
        public Guid ShipmentId { get; init; }
        public Guid ProposalId { get; init; }
        public string? ShipmentCode { get; init; }
        public string? ProposalCode { get; init; }

        // Sender
        public string SenderName { get; init; } = string.Empty;
        public string SenderPhone { get; init; } = string.Empty;
        public string PickupAddress { get; init; } = string.Empty;

        // Receiver
        public string ReceiverName { get; init; } = string.Empty;
        public string ReceiverPhone { get; init; } = string.Empty;
        public string DestAddress { get; init; } = string.Empty;

        // Shipment details
        public string Category { get; init; } = string.Empty;
        public string? Description { get; init; }
        public decimal WeightKg { get; init; }
        public decimal VolumeCbm { get; init; }
        public int Quantity { get; init; }
        public bool CodRequired { get; init; }
        public string? Note { get; init; }
        public decimal CodAmount { get; init; }

        // Statuses
        public string ProposalStatus { get; init; } = string.Empty;
        public string ShipmentStatus { get; init; } = string.Empty;
        public string? QuotationStatus { get; init; }
        public string? PaymentStatus { get; init; }

        // Quotation info (if available)
        public decimal? ShippingFee { get; init; }
        public decimal? DepositAmount { get; init; }

        // Trip info
        public Guid TripId { get; init; }
        public string? TripCode { get; init; }
        public string? Origin { get; init; }
        public string? Destination { get; init; }

        public DateTime CreatedAt { get; init; }

        // Tracking timeline
        public List<ExternalShipmentTimelineEntry> Timeline { get; init; } = new();
    }

    /// <summary>
    /// A single timeline entry for external shipment tracking.
    /// </summary>
    public sealed record ExternalShipmentTimelineEntry
    {
        public string Action { get; init; } = string.Empty;
        public string? Details { get; init; }
        public DateTime OccurredAt { get; init; }
        public bool IsCompleted { get; init; }
        public bool IsCurrent { get; init; }
    }
}
