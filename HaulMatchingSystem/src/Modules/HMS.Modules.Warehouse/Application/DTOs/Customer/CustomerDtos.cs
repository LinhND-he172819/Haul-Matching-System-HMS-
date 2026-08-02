namespace HMS.Modules.Warehouse.Application.DTOs.Customer;

// ─── Customer Shipment List ────────────────────────────────────────────

public sealed record CustomerShipmentListItem
{
    public Guid Id { get; init; }
    public string ShipmentCode { get; init; } = string.Empty;
    public string? Commodity { get; init; }
    public decimal Weight { get; init; }
    public decimal Volume { get; init; }
    public string? ReceiverName { get; init; }
    public string? DeliveryAddress { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? TripCode { get; init; }
    public string? OriginName { get; init; }
    public string? DestinationName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public AllowedActions AllowedActions { get; init; } = new();
}

public sealed record AllowedActions
{
    public bool CanView { get; init; }
    public bool CanEdit { get; init; }
    public bool CanCancel { get; init; }
    public bool CanPayDeposit { get; init; }
    public bool CanPayRemaining { get; init; }
}

// ─── Customer Shipment Detail ──────────────────────────────────────────

public sealed record CustomerShipmentDetail
{
    public Guid Id { get; init; }
    public string ShipmentCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    // Sender info (from proposal or draft)
    public string? SenderName { get; init; }
    public string? SenderPhone { get; init; }
    public string? PickupAddress { get; init; }
    public string? PickupNote { get; init; }

    // Receiver info
    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryAddress { get; init; }

    // Cargo info
    public string? Commodity { get; init; }
    public decimal Weight { get; init; }
    public decimal Volume { get; init; }
    public string? SpecialInstructions { get; init; }

    // Trip info (if linked)
    public string? TripCode { get; init; }
    public string? OriginName { get; init; }
    public string? DestinationName { get; init; }
    public DateTimeOffset? DepartureTime { get; init; }
    public string? VehiclePlate { get; init; }

    // Proposal info
    public ProposalInfo? Proposal { get; init; }

    // Quotation info
    public QuotationInfo? Quotation { get; init; }

    // Payment summary
    public PaymentSummary? Payment { get; init; }

    // Timeline
    public List<TimelineEntry> Timeline { get; init; } = new();

    // Actions
    public AllowedActions AllowedActions { get; init; } = new();
}

public sealed record ProposalInfo
{
    public Guid? Id { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public string? RejectReason { get; init; }
}

public sealed record QuotationInfo
{
    public Guid? Id { get; init; }
    public string? QuotationCode { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal DepositAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Status { get; init; }
}

public sealed record PaymentSummary
{
    public decimal DepositPaid { get; init; }
    public decimal FinalPaid { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal OutstandingAmount { get; init; }
    public string? TransactionRef { get; init; }
    public string? PaymentStatus { get; init; }
}

public sealed record TimelineEntry
{
    public string Label { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsCurrent { get; init; }
}

// ─── Update Draft Request ──────────────────────────────────────────────

public sealed record UpdateDraftShipmentRequest
{
    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryAddress { get; init; }
    public double? DeliveryLatitude { get; init; }
    public double? DeliveryLongitude { get; init; }
    public string? Commodity { get; init; }
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public string? SpecialInstructions { get; init; }

    // Pickup info (for draft)
    public string? SenderName { get; init; }
    public string? SenderPhone { get; init; }
    public string? PickupAddress { get; init; }
    public double? PickupLatitude { get; init; }
    public double? PickupLongitude { get; init; }
    public string? PickupNote { get; init; }
}

// ─── Cancel Request ────────────────────────────────────────────────────

public sealed record CancelShipmentRequest
{
    public string Reason { get; init; } = string.Empty;
}

// ─── Paged Result ──────────────────────────────────────────────────────

public sealed record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}
