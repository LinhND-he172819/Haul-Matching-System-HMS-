namespace HMS.Modules.Warehouse.Application.DTOs;

public class CreateDraftShipmentRequest
{
    public Guid CustomerId { get; set; }

    // ── Sender fields (required for DirectPickup, optional for Hub) ──
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? PickupAddress { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public string? PickupNote { get; set; }

    // ── Cargo fields ──
    public string CargoType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal VolumeCbm { get; set; }

    // ── Receiver fields ──
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DestAddress { get; set; } = string.Empty;

    public double DestLat { get; set; }
    public double DestLng { get; set; }

    public string? SpecialHandlingNote { get; set; }
}