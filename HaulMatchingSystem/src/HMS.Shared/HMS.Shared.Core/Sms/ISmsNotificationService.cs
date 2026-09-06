namespace HMS.Shared.Core.Sms;

/// <summary>
/// Business-level SMS notification service.
/// Resolves customer phone, builds messages, sends SMS, and handles failures.
/// Business logic depends on this interface — not on ISmsSender directly.
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Sends a quotation-available SMS to the customer who owns the given proposal's shipment.
    /// Resolves customer phone from the database using the proposal's customer_id.
    /// </summary>
    /// <param name="customerId">The customer who owns the shipment.</param>
    /// <param name="shipmentCode">Shipment code (e.g. SHP-20250818-0001).</param>
    /// <param name="shippingFee">Shipping fee amount.</param>
    /// <param name="currency">Currency code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>SmsSendResult with outcome details.</returns>
    Task<SmsSendResult> SendQuotationAvailableAsync(
        Guid customerId,
        string shipmentCode,
        decimal shippingFee,
        string currency,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a shipment status update SMS to the customer who owns the shipment.
    /// Resolves customer phone from the database using shipment_id.
    /// </summary>
    /// <param name="shipmentId">Shipment ID.</param>
    /// <param name="messageType">Type of SMS message.</param>
    /// <param name="shipmentCode">Shipment code for the message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>SmsSendResult with outcome details.</returns>
    Task<SmsSendResult> SendShipmentStatusAsync(
        Guid shipmentId,
        SmsMessageType messageType,
        string shipmentCode,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a shipment status update SMS using pre-resolved customer info.
    /// Used when the caller already has the customer phone (e.g. from the same transaction).
    /// </summary>
    /// <param name="customerId">Customer ID who should receive the SMS.</param>
    /// <param name="messageType">Type of SMS message.</param>
    /// <param name="shipmentCode">Shipment code for the message.</param>
    /// <param name="shippingFee">Optional shipping fee (for quotation messages).</param>
    /// <param name="currency">Optional currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>SmsSendResult with outcome details.</returns>
    Task<SmsSendResult> SendShipmentStatusAsync(
        Guid customerId,
        SmsMessageType messageType,
        string shipmentCode,
        decimal? shippingFee = null,
        string? currency = null,
        CancellationToken ct = default);
}
