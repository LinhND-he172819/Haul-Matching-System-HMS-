namespace HMS.Shared.Core.Sms;

/// <summary>
/// Centralized SMS message templates for HMS notifications.
/// All SMS content generation should go through this class.
/// Templates use string interpolation with named placeholders.
/// </summary>
public static class SmsMessageTemplates
{
    /// <summary>
    /// Format: shipment code in the message.
    /// </summary>
    public static string QuotationAvailable(string shipmentCode, decimal shippingFee, string currency = "VND")
    {
        return $"HMS: Ban co bao gia moi cho don {shipmentCode}. " +
               $"Phi van chuyen: {shippingFee:N0} {currency}. " +
               $"Vui long dang nhap HMS de xem chi tiet.";
    }

    public static string ShipmentPendingDeposit(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} dang cho dat coc. " +
               $"Vui long dang nhap HMS de xem thong tin thanh toan.";
    }

    public static string ShipmentMatched(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} da duoc ghep chuyen thanh cong.";
    }

    public static string ShipmentInTransit(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} dang duoc van chuyen.";
    }

    public static string ShipmentDelivered(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} da duoc giao den nguoi nhan.";
    }

    public static string ShipmentCompleted(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} da hoan tat.";
    }

    public static string ShipmentCancelled(string shipmentCode)
    {
        return $"HMS: Don {shipmentCode} da bi huy. " +
               $"Vui long dang nhap HMS de xem chi tiet.";
    }

    /// <summary>
    /// Builds an SMS message based on message type and parameters.
    /// </summary>
    public static string BuildMessage(SmsMessageType messageType, SmsMessageParams p)
    {
        return messageType switch
        {
            SmsMessageType.QuotationAvailable => QuotationAvailable(
                p.ShipmentCode ?? "N/A", p.ShippingFee ?? 0, p.Currency ?? "VND"),
            SmsMessageType.ShipmentPendingDeposit => ShipmentPendingDeposit(p.ShipmentCode ?? "N/A"),
            SmsMessageType.ShipmentMatched => ShipmentMatched(p.ShipmentCode ?? "N/A"),
            SmsMessageType.ShipmentInTransit => ShipmentInTransit(p.ShipmentCode ?? "N/A"),
            SmsMessageType.ShipmentDelivered => ShipmentDelivered(p.ShipmentCode ?? "N/A"),
            SmsMessageType.ShipmentCompleted => ShipmentCompleted(p.ShipmentCode ?? "N/A"),
            SmsMessageType.ShipmentCancelled => ShipmentCancelled(p.ShipmentCode ?? "N/A"),
            _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unknown SMS message type")
        };
    }
}

/// <summary>
/// Parameters for building SMS messages.
/// </summary>
public class SmsMessageParams
{
    public string? ShipmentCode { get; set; }
    public decimal? ShippingFee { get; set; }
    public string? Currency { get; set; }
}
