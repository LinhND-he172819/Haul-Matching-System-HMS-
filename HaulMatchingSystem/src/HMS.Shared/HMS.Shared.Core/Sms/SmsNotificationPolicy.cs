using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Sms;

/// <summary>
/// Centralized policy defining which shipment statuses trigger SMS notifications.
/// All SMS trigger logic should reference this policy instead of scattered if-statements.
/// </summary>
public static class SmsNotificationPolicy
{
    /// <summary>
    /// Set of shipment statuses that should trigger an SMS to the customer.
    /// </summary>
    private static readonly HashSet<ShipmentStatus> CustomerNotifyStatuses = new()
    {
        ShipmentStatus.Matched,
        ShipmentStatus.In_Transit,
        ShipmentStatus.Delivered,
        ShipmentStatus.Completed,
        ShipmentStatus.Cancelled
    };

    /// <summary>
    /// Determines whether a shipment status transition should trigger an SMS notification.
    /// SMS is sent only when:
    /// 1. oldStatus != newStatus (no duplicate for same status)
    /// 2. newStatus is in the customer-facing status set
    /// </summary>
    /// <param name="oldStatus">Previous shipment status.</param>
    /// <param name="newStatus">New shipment status.</param>
    /// <returns>True if an SMS should be sent.</returns>
    public static bool ShouldNotifyCustomer(ShipmentStatus oldStatus, ShipmentStatus newStatus)
    {
        if (oldStatus == newStatus)
            return false;

        return CustomerNotifyStatuses.Contains(newStatus);
    }

    /// <summary>
    /// Determines whether a shipment status transition should trigger an SMS notification,
    /// using string status values (for controllers that bypass the enum-based state machine).
    /// </summary>
    public static bool ShouldNotifyCustomer(string oldStatus, string newStatus)
    {
        if (!Enum.TryParse<ShipmentStatus>(oldStatus, out var oldParsed))
            return false;
        if (!Enum.TryParse<ShipmentStatus>(newStatus, out var newParsed))
            return false;

        return ShouldNotifyCustomer(oldParsed, newParsed);
    }

    /// <summary>
    /// Maps a shipment status to the corresponding SMS message type.
    /// Returns null if the status does not have an SMS message type.
    /// </summary>
    public static SmsMessageType? GetMessageType(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Matched => SmsMessageType.ShipmentMatched,
            ShipmentStatus.In_Transit => SmsMessageType.ShipmentInTransit,
            ShipmentStatus.Delivered => SmsMessageType.ShipmentDelivered,
            ShipmentStatus.Completed => SmsMessageType.ShipmentCompleted,
            ShipmentStatus.Cancelled => SmsMessageType.ShipmentCancelled,
            _ => null
        };
    }

    /// <summary>
    /// Maps a string shipment status to the corresponding SMS message type.
    /// </summary>
    public static SmsMessageType? GetMessageType(string status)
    {
        if (!Enum.TryParse<ShipmentStatus>(status, out var parsed))
            return null;
        return GetMessageType(parsed);
    }
}
