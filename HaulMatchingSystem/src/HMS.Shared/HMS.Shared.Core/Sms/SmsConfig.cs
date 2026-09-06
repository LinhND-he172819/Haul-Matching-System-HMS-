namespace HMS.Shared.Core.Sms;

/// <summary>
/// SMS configuration loaded from appsettings / environment variables.
/// Supports Mock mode for development and Provider mode for production.
/// </summary>
public class SmsConfig
{
    /// <summary>Configuration section name: "Sms".</summary>
    public const string SectionName = "Sms";

    /// <summary>Master switch. If false, no SMS is sent (business flow unaffected).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Operating mode: "Mock" for development, "Provider" for production.
    /// In Mock mode, SMS is logged but not actually sent.
    /// </summary>
    public string Mode { get; set; } = "Mock";

    /// <summary>Provider name for logging/reference (e.g. "eSMS", "Twilio").</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Provider API base URL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Provider API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Provider secret.</summary>
    public string? Secret { get; set; }

    /// <summary>Brand name / sender ID.</summary>
    public string? Sender { get; set; }

    /// <summary>HTTP timeout in seconds for provider calls.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Maximum number of retry attempts for transient failures.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Returns true if running in mock mode.</summary>
    public bool IsMock => string.Equals(Mode, "Mock", StringComparison.OrdinalIgnoreCase);

    /// <summary>Resolves the effective mode based on config and available credentials.</summary>
    public bool ShouldSend => Enabled && !IsMock;
}
