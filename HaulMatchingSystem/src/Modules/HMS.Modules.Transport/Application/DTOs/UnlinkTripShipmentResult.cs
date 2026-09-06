namespace HMS.Modules.Transport.Application.DTOs;

/// <summary>
/// Result of unlinking a single shipment from a trip.
/// Includes the updated trip so the UI can refresh without an extra round-trip,
/// plus a list of shipment IDs that were auto-unlinked when the trip was cancelled.
/// </summary>
public sealed record UnlinkTripShipmentResult(
    TripResponse Trip,
    string UnlinkedShipmentQrCode,
    IReadOnlyList<string> AlsoUnlinkedOnCancel);
