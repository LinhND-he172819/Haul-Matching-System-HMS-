using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Models;

/// <summary>
/// A single record in the shipment_status_history audit table.
/// </summary>
public sealed class ShipmentStatusHistoryDto
{
    public Guid Id { get; init; }
    public Guid ShipmentId { get; init; }
    public ShipmentStatus FromStatus { get; init; }
    public ShipmentStatus ToStatus { get; init; }
    public Guid? PerformedBy { get; init; }
    public string? PerformedByName { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
