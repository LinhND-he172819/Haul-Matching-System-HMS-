namespace HMS.Shared.Core.Interfaces;

/// <summary>
/// Abstraction for sending emails. Implementations can use SMTP, SendGrid, Resend, etc.
/// Business logic must depend on this interface, never on a concrete implementation.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email. This method must NOT throw on failure; it should log and swallow.
    /// Callers should never need to wrap this in try/catch for business flow.
    /// </summary>
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>
    /// Sends an email to multiple recipients. Each recipient gets their own copy.
    /// Duplicate addresses (case-insensitive) should only receive one email.
    /// This method must NOT throw on failure.
    /// </summary>
    Task SendToManyAsync(IEnumerable<string> toAddresses, string subject, string htmlBody, CancellationToken ct = default);
}
