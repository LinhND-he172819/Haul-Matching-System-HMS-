using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Sms;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Shared.Infrastructure.Services;

/// <summary>
/// Business-level SMS notification service.
/// Resolves customer phone from database, builds messages, sends SMS via ISmsSender,
/// and logs results to shared.sms_logs.
/// 
/// Flow:
/// 1. Resolve customer phone from identity.users
/// 2. Normalize phone to international format
/// 3. Check config (enabled/disabled)
/// 4. Build message from templates
/// 5. Send via ISmsSender (low-level provider)
/// 6. Log result (success/failure)
/// </summary>
public class SmsNotificationService : HMS.Shared.Core.Sms.ISmsNotificationService
{
    private readonly ISmsSender _smsSender;
    private readonly SmsConfig _config;
    private readonly SmsLogRepository _logRepo;
    private readonly string _connStr;
    private readonly ILogger<SmsNotificationService> _logger;

    public SmsNotificationService(
        ISmsSender smsSender,
        SmsConfig config,
        SmsLogRepository logRepo,
        string connectionString,
        ILogger<SmsNotificationService> logger)
    {
        _smsSender = smsSender;
        _config = config;
        _logRepo = logRepo;
        _connStr = connectionString;
        _logger = logger;
    }

    public async Task<SmsSendResult> SendQuotationAvailableAsync(
        Guid customerId,
        string shipmentCode,
        decimal shippingFee,
        string currency,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("SMS disabled by configuration.");
            return SmsSendResult.Fail("DISABLED", "SMS disabled by configuration");
        }

        // Resolve customer phone
        var phone = await ResolveCustomerPhoneAsync(customerId, ct);
        if (phone == null)
        {
            _logger.LogWarning(
                "Customer {CustomerId} has no valid phone number. Skipping QuotationAvailable SMS for {ShipmentCode}.",
                customerId, shipmentCode);
            return SmsSendResult.Fail("NO_PHONE", "Customer has no valid phone number");
        }

        var messageType = SmsMessageType.QuotationAvailable;
        var message = SmsMessageTemplates.BuildMessage(messageType, new SmsMessageParams
        {
            ShipmentCode = shipmentCode,
            ShippingFee = shippingFee,
            Currency = currency
        });

        return await SendAsync(phone, message, messageType.ToString(),
            "Quotation", null, ct);
    }

    public async Task<SmsSendResult> SendShipmentStatusAsync(
        Guid shipmentId,
        SmsMessageType messageType,
        string shipmentCode,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("SMS disabled by configuration.");
            return SmsSendResult.Fail("DISABLED", "SMS disabled by configuration");
        }

        // Resolve customer from shipment
        var (customerId, phone) = await ResolveShipmentCustomerAsync(shipmentId, ct);
        if (phone == null)
        {
            _logger.LogWarning(
                "Shipment {ShipmentCode} ({ShipmentId}): Customer has no valid phone number. Skipping {MessageType} SMS.",
                shipmentCode, shipmentId, messageType);
            return SmsSendResult.Fail("NO_PHONE", "Customer has no valid phone number");
        }

        // Check duplicate: was the same SMS sent recently?
        if (await _logRepo.WasRecentlySentAsync("Shipment", shipmentId, messageType.ToString(), withinSeconds: 300, ct))
        {
            _logger.LogInformation(
                "Duplicate SMS prevented: {MessageType} for Shipment {ShipmentCode} was sent recently.",
                messageType, shipmentCode);
            return SmsSendResult.Fail("DUPLICATE", "SMS was sent recently");
        }

        var message = SmsMessageTemplates.BuildMessage(messageType, new SmsMessageParams
        {
            ShipmentCode = shipmentCode
        });

        return await SendAsync(phone, message, messageType.ToString(),
            "Shipment", shipmentId, ct);
    }

    public async Task<SmsSendResult> SendShipmentStatusAsync(
        Guid customerId,
        SmsMessageType messageType,
        string shipmentCode,
        decimal? shippingFee = null,
        string? currency = null,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("SMS disabled by configuration.");
            return SmsSendResult.Fail("DISABLED", "SMS disabled by configuration");
        }

        var phone = await ResolveCustomerPhoneAsync(customerId, ct);
        if (phone == null)
        {
            _logger.LogWarning(
                "Customer {CustomerId} has no valid phone number. Skipping {MessageType} SMS.",
                customerId, messageType);
            return SmsSendResult.Fail("NO_PHONE", "Customer has no valid phone number");
        }

        var message = SmsMessageTemplates.BuildMessage(messageType, new SmsMessageParams
        {
            ShipmentCode = shipmentCode,
            ShippingFee = shippingFee,
            Currency = currency
        });

        return await SendAsync(phone, message, messageType.ToString(),
            null, null, ct);
    }

    // ─── Private helpers ───────────────────────────────────────────

    private async Task<SmsSendResult> SendAsync(
        string phone, string message, string messageType,
        string? relatedEntityType, Guid? relatedEntityId,
        CancellationToken ct)
    {
        // Log to database
        var logId = await _logRepo.InsertAsync(
            phone, messageType, relatedEntityType, relatedEntityId,
            message, _config.Provider, "Pending", ct);

        try
        {
            if (_config.IsMock)
            {
                // Mock mode: log and return success
                _logger.LogInformation(
                    "[MOCK SMS] To: {Phone} | Type: {Type} | Message: {Message}",
                    PhoneHelper.Mask(phone), messageType, message);
                await _logRepo.UpdateSendResultAsync(logId, true, providerMessageId: null, ct: ct);
                return SmsSendResult.Ok();
            }

            // Send via provider
            _logger.LogInformation(
                "Sending SMS via {Provider} to {Phone}, type={Type}",
                _config.Provider, PhoneHelper.Mask(phone), messageType);

            await _smsSender.SendSmsAsync(phone, message);

            await _logRepo.UpdateSendResultAsync(logId, true, ct: ct);
            return SmsSendResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMS send failed: type={Type}, phone={Phone}, error={Error}",
                messageType, PhoneHelper.Mask(phone), ex.Message);

            await _logRepo.UpdateSendResultAsync(logId, false,
                errorMessage: ex.Message, ct: ct);

            return SmsSendResult.Fail("SEND_ERROR", ex.Message);
        }
    }

    private async Task<string?> ResolveCustomerPhoneAsync(Guid customerId, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT phone FROM identity.users
                WHERE id = @id AND is_deleted = FALSE;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", customerId);
            var result = await cmd.ExecuteScalarAsync(ct);

            if (result == null || result == DBNull.Value)
                return null;

            var rawPhone = result.ToString();
            return PhoneHelper.Normalize(rawPhone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve phone for customer {CustomerId}", customerId);
            return null;
        }
    }

    private async Task<(Guid? CustomerId, string? Phone)> ResolveShipmentCustomerAsync(
        Guid shipmentId, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT s.customer_id, u.phone
                FROM warehouse.shipments s
                JOIN identity.users u ON u.id = s.customer_id AND u.is_deleted = FALSE
                WHERE s.id = @id AND s.is_deleted = FALSE;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", shipmentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return (null, null);

            var customerId = reader.GetGuid(0);
            var rawPhone = reader.IsDBNull(1) ? null : reader.GetString(1);
            var phone = PhoneHelper.Normalize(rawPhone);

            return (customerId, phone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve customer for shipment {ShipmentId}", shipmentId);
            return (null, null);
        }
    }
}
