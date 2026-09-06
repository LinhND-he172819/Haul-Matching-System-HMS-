namespace HMS.Shared.Core.Sms;

/// <summary>
/// Structured result from an SMS send operation.
/// Used for logging and tracking without throwing exceptions for provider failures.
/// </summary>
public class SmsSendResult
{
    public bool Success { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static SmsSendResult Ok(string? providerMessageId = null) => new()
    {
        Success = true,
        ProviderMessageId = providerMessageId
    };

    public static SmsSendResult Fail(string? errorCode = null, string? errorMessage = null) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
