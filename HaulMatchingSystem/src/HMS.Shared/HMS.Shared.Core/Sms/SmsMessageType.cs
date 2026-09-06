namespace HMS.Shared.Core.Sms;

/// <summary>
/// Standardized message types for SMS notifications.
/// Used for logging, tracking, and template selection.
/// </summary>
public enum SmsMessageType
{
    /// <summary>Quotation has been sent to customer (Draft → Sent).</summary>
    QuotationAvailable,

    /// <summary>Shipment deposit is pending (QuotationSent → PendingDeposit).</summary>
    ShipmentPendingDeposit,

    /// <summary>Shipment has been matched with a driver.</summary>
    ShipmentMatched,

    /// <summary>Shipment is in transit.</summary>
    ShipmentInTransit,

    /// <summary>Shipment has been delivered to receiver.</summary>
    ShipmentDelivered,

    /// <summary>Shipment is fully completed (final payment received).</summary>
    ShipmentCompleted,

    /// <summary>Shipment has been cancelled.</summary>
    ShipmentCancelled
}
