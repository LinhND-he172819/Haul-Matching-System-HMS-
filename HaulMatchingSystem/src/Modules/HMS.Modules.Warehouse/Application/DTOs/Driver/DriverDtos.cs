namespace HMS.Modules.Warehouse.Application.DTOs.Driver;

// ─── Driver Trip List ──────────────────────────────────────────────────

public sealed record DriverTripListItem
{
    public Guid Id { get; init; }
    public string TripCode { get; init; } = string.Empty;
    public string? OriginName { get; init; }
    public string? DestinationName { get; init; }
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>Ngày khởi hành dự kiến (scheduled departure). Giữ nguyên DepartureTime = thời điểm đi thực tế.</summary>
    public DateTimeOffset? ScheduledDepartureAt { get; init; }

    public string? VehiclePlate { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalShipments { get; init; }
    public decimal CurrentWeight { get; init; }
    public decimal RemainingWeight { get; init; }
    public decimal CurrentVolume { get; init; }
    public decimal RemainingVolume { get; init; }
    public DriverAllowedActions AllowedActions { get; init; } = new();
}

public sealed record DriverAllowedActions
{
    public bool CanView { get; init; }
    public bool CanStart { get; init; }
    public bool CanComplete { get; init; }
}

// ─── Driver Trip Detail ────────────────────────────────────────────────

public sealed record DriverTripDetail
{
    public Guid Id { get; init; }
    public string TripCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>Ngày khởi hành dự kiến (scheduled departure). DepartureTime = thời điểm đi thực tế.</summary>
    public DateTimeOffset? ScheduledDepartureAt { get; init; }

    public string? VehiclePlate { get; init; }
    public string? OriginName { get; init; }
    public string? DestinationName { get; init; }
    public string? RouteLineString { get; init; }

    // Capacity
    public decimal CurrentWeight { get; init; }
    public decimal RemainingWeight { get; init; }
    public decimal MaxWeight { get; init; }
    public decimal CurrentVolume { get; init; }
    public decimal RemainingVolume { get; init; }
    public decimal MaxVolume { get; init; }

    // Shipments summary
    public int TotalShipments { get; init; }
    public List<DriverShipmentListItem> Shipments { get; init; } = new();

    // Timeline
    public List<TripTimelineEntry> Timeline { get; init; } = new();

    // Actions
    public DriverAllowedActions AllowedActions { get; init; } = new();
}

// ─── Driver Shipment in Trip ───────────────────────────────────────────

public sealed record DriverShipmentListItem
{
    public Guid Id { get; init; }
    public string ShipmentCode { get; init; } = string.Empty;
    public string? Commodity { get; init; }
    public decimal Weight { get; init; }
    public decimal Volume { get; init; }
    public string Status { get; init; } = string.Empty;

    public string? SenderName { get; init; }
    public string? SenderPhone { get; init; }
    public string? PickupAddress { get; init; }

    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryAddress { get; init; }

    /// <summary>When status is Delivered and a pending FinalPayment (COD) exists, this is its ID.</summary>
    public Guid? PendingCodPaymentId { get; init; }

    /// <summary>COD amount to collect from receiver (null if no pending COD).</summary>
    public decimal? PendingCodAmount { get; init; }
    /// <summary>Currency of the COD payment.</summary>
    public string? PendingCodCurrency { get; init; }
    /// <summary>Payment code for the pending COD payment.</summary>
    public string? PendingCodPaymentCode { get; init; }

    public DriverShipmentAllowedActions AllowedActions { get; init; } = new();
}

public sealed record DriverShipmentAllowedActions
{
    public bool CanView { get; init; }
    public bool CanConfirmPickup { get; init; }
    public bool CanStartTransport { get; init; }
    public bool CanConfirmDelivery { get; init; }
    public bool CanConfirmCod { get; init; }
}

// ─── Driver Shipment Detail ────────────────────────────────────────────

public sealed record DriverShipmentDetail
{
    public Guid Id { get; init; }
    public string ShipmentCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    // Cargo
    public string? Commodity { get; init; }
    public decimal Weight { get; init; }
    public decimal Volume { get; init; }
    public string? SpecialInstructions { get; init; }

    // Sender
    public string? SenderName { get; init; }
    public string? SenderPhone { get; init; }
    public string? PickupAddress { get; init; }
    public string? PickupNote { get; init; }

    // Receiver
    public string? ReceiverName { get; init; }
    public string? ReceiverPhone { get; init; }
    public string? DeliveryAddress { get; init; }

    // Timeline
    public List<TimelineEntry> Timeline { get; init; } = new();

    /// <summary>When status is Delivered and a pending FinalPayment (COD) exists, this is its ID.</summary>
    public Guid? PendingCodPaymentId { get; init; }

    /// <summary>COD amount to collect from receiver (null if no pending COD).</summary>
    public decimal? PendingCodAmount { get; init; }
    /// <summary>Currency of the COD payment.</summary>
    public string? PendingCodCurrency { get; init; }
    /// <summary>Payment code for the pending COD payment.</summary>
    public string? PendingCodPaymentCode { get; init; }

    // Actions
    public DriverShipmentAllowedActions AllowedActions { get; init; } = new();
}

public sealed record TimelineEntry
{
    public string Label { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsCurrent { get; init; }
}

// ─── Trip Timeline ─────────────────────────────────────────────────────

public sealed record TripTimelineEntry
{
    public string Label { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsCurrent { get; init; }
}

// ─── Confirm Pickup Request ────────────────────────────────────────────

public sealed record ConfirmPickupRequest
{
    public string? PickupNote { get; init; }
}

// ─── Start Transport Request ───────────────────────────────────────────

public sealed record StartTransportRequest
{
    public string? Note { get; init; }
}

// ─── Confirm Delivery Request ──────────────────────────────────────────

public sealed record ConfirmDeliveryRequest
{
    public string? DeliveryNote { get; init; }
    public string? ProofImageUrl { get; init; }
}

// ─── Report Incident Request ───────────────────────────────────────────

public sealed record ReportIncidentRequest
{
    public Guid? ShipmentId { get; init; }
    public string IncidentType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

// ─── Incident Type Constants ────────────────────────────────────────

public static class IncidentTypes
{
    public const string Delay = "Delay";
    public const string VehicleBreakdown = "VehicleBreakdown";
    public const string Accident = "Accident";
    public const string CargoDamage = "CargoDamage";
    public const string CargoLost = "CargoLost";
    public const string DeliveryProblem = "DeliveryProblem";
    public const string RouteProblem = "RouteProblem";
    public const string Weather = "Weather";
    public const string Other = "Other";

    public static readonly HashSet<string> Allowed = new()
    {
        Delay, VehicleBreakdown, Accident, CargoDamage, CargoLost,
        DeliveryProblem, RouteProblem, Weather, Other
    };
}
