namespace HMS.Modules.Warehouse.Application.DTOs;

public sealed class ConfirmIntakeRequest
{
    public decimal ActualWeightKg { get; set; }
    public decimal ActualVolumeCbm { get; set; }
}
