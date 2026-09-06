namespace HMS.Shared.Core.Sms;

/// <summary>
/// Represents a record of an SMS notification for audit and tracking purposes.
/// Stored in shared.sms_logs table.
/// </summary>
public class SmsLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RecipientPhone { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string Message { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string? ProviderMessageId { get; set; }
    public string Status { get; set; } = nameof(SmsLogStatus.Pending);
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
