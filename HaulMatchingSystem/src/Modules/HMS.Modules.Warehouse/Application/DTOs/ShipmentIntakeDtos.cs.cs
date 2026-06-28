namespace HMS.Modules.Warehouse.Application.DTOs;

public class ShipmentQrLookupResponse
{
    public Guid Id { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string CargoType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal VolumeCbm { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DestAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SpecialHandlingNote { get; set; }
}

public class ConfirmIntakeRequest
{
    public decimal ActualWeightKg { get; set; }
    public decimal ActualVolumeCbm { get; set; }
}