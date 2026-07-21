namespace HMS.Modules.Warehouse.Application.DTOs;

public class HubInventoryDetailDto
{
    // Shipment info
    public Guid Id { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? CurrentHubId { get; set; }
    public string? CurrentHubName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IntakeConfirmedAt { get; set; }

    // Customer (sender) info
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    // Receiver info
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DestAddress { get; set; } = string.Empty;

    // Cargo info
    public string CargoType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal VolumeCbm { get; set; }
    public decimal? Cod { get; set; }
    public decimal? ShippingFee { get; set; }
    public string? SpecialHandlingNote { get; set; }

    // Hub intake info
    public string? IntakeStaffName { get; set; }
    public DateTime? IntakeConfirmedAt2 { get; set; }
    public int DaysInWarehouse { get; set; }

    // Timeline
    public List<TimelineEntry> Timeline { get; set; } = new();
}

public class TimelineEntry
{
    public string Label { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
}
