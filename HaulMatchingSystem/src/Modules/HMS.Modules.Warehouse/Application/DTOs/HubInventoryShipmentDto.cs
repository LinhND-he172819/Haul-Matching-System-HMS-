namespace HMS.Modules.Warehouse.Application.DTOs;

public class HubInventoryShipmentDto
{
    public Guid Id { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string CargoType { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DestAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? CurrentHubId { get; set; }
    public string? CurrentHubName { get; set; }
    public decimal WeightKg { get; set; }
    public decimal VolumeCbm { get; set; }
    public decimal? Cod { get; set; }
    public decimal? ShippingFee { get; set; }
    public string? SpecialHandlingNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IntakeConfirmedAt { get; set; }
    public int DaysInWarehouse { get; set; }
}
