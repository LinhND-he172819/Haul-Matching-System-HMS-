namespace HMS.Modules.Warehouse.Application.DTOs;

public class UpdateShipmentRequest
{
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? DestAddress { get; set; }
    public string? CargoType { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? VolumeCbm { get; set; }
    public string? SpecialHandlingNote { get; set; }
}
