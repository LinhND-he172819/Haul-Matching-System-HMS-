namespace HMS.Modules.Transport.Application.DTOs;

/// <summary>
/// Lightweight shipment DTO returned by the admin GET /api/trips/{id}/shipments endpoint
/// and the unlink endpoint. Includes the shipment's current status so the UI can show
/// whether each shipment is still Matched, already In_Transit, or Delivered.
/// </summary>
public sealed record TripShipmentResponse(
    Guid ShipmentId,
    string QrCode,
    string? CargoType,
    decimal WeightKg,
    decimal VolumeCbm,
    string Status,
    string? SenderName,
    string? SenderPhone,
    string? PickupAddress,
    string? ReceiverName,
    string? ReceiverPhone,
    string? DeliveryAddress);
