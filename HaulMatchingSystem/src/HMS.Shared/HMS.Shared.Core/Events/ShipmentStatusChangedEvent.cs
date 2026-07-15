using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Events;

/// <summary>
/// Published after every successful shipment status transition.
/// Modules subscribe via MediatR to react (notifications, analytics, etc.).
/// </summary>
public sealed class ShipmentStatusChangedEvent : MediatR.INotification
{
    public Guid ShipmentId { get; init; }
    public ShipmentStatus FromStatus { get; init; }
    public ShipmentStatus ToStatus { get; init; }
    public Guid? PerformedBy { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
