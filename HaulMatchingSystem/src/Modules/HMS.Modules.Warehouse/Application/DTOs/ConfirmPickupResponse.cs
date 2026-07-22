namespace HMS.Modules.Warehouse.Application.DTOs;

public sealed class ConfirmPickupResponse
{
    public Guid ShipmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PickedUpBy { get; set; }
    public DateTimeOffset PickedUpAt { get; set; }
}
