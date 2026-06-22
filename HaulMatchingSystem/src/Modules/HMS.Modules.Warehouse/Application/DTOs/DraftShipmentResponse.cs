namespace HMS.Modules.Warehouse.Application.DTOs;

public class DraftShipmentResponse
{
    public Guid Id { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}