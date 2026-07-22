using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Exceptions;

/// <summary>
/// Thrown when an invalid shipment status transition is attempted.
/// </summary>
public sealed class InvalidShipmentTransitionException : InvalidOperationException
{
    public ShipmentStatus FromStatus { get; }
    public ShipmentStatus ToStatus { get; }

    public InvalidShipmentTransitionException(ShipmentStatus from, ShipmentStatus to, string? detail = null)
        : base(FormatMessage(from, to, detail))
    {
        FromStatus = from;
        ToStatus = to;
    }

    private static string FormatMessage(ShipmentStatus from, ShipmentStatus to, string? detail)
    {
        var msg = $"Invalid shipment transition: {from} → {to}.";
        if (!string.IsNullOrEmpty(detail))
            msg += $" {detail}";
        return msg;
    }
}
