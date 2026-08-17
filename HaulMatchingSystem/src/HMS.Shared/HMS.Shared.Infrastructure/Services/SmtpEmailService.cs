using System.Net;
using System.Net.Mail;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.Shared.Infrastructure.Services;

/// <summary>
/// SMTP-based email service. Configuration comes from appsettings.json under "Email" section.
/// Never throws on failure — logs errors and swallows exceptions to prevent email failure
/// from breaking business operations.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _settings = configuration.GetSection("Email").Get<SmtpSettings>() ?? new SmtpSettings();
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        await SendToManyAsync(new[] { toAddress }, subject, htmlBody, ct);
    }

    public async Task SendToManyAsync(IEnumerable<string> toAddresses, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping send to {Count} recipients.", toAddresses.Count());
            return;
        }

        // Deduplicate addresses (case-insensitive)
        var uniqueAddresses = toAddresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (uniqueAddresses.Count == 0)
        {
            _logger.LogWarning("No valid email addresses to send to for subject: {Subject}", subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = _settings.UseSsl,
                Timeout = _settings.TimeoutSeconds * 1000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            foreach (var addr in uniqueAddresses)
            {
                message.To.Add(addr);
            }

            await client.SendMailAsync(message);

            _logger.LogInformation(
                "Email sent successfully. Subject: {Subject}, Recipients: {Count}",
                subject, uniqueAddresses.Count);
        }
        catch (Exception ex)
        {
            // CRITICAL: Email failure must NOT throw. Log only.
            _logger.LogError(ex,
                "Failed to send email. Subject: {Subject}, Recipients: {Count}. Error: {Error}",
                subject, uniqueAddresses.Count, ex.Message);
        }
    }
}

/// <summary>
/// SMTP configuration settings. Values come from appsettings.json → "Email" section.
/// </summary>
public sealed class SmtpSettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "HMS";
}
