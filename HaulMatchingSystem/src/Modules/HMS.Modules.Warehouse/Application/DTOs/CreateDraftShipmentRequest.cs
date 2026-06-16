namespace HMS.Modules.Warehouse.Application.DTOs;

public class CreateDraftShipmentRequest
{
    public Guid CustomerId { get; set; }

    public string CargoType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal VolumeCbm { get; set; }

    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DestAddress { get; set; } = string.Empty;

    public double DestLat { get; set; }
    public double DestLng { get; set; }

    public string? SpecialHandlingNote { get; set; }
}