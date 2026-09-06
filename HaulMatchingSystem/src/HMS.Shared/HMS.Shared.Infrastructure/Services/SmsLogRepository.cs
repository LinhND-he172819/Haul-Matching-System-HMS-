using HMS.Shared.Core.Sms;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Shared.Infrastructure.Services;

/// <summary>
/// Repository for SMS log operations. Uses raw Npgsql for consistency with project patterns.
/// Table: shared.sms_logs
/// </summary>
public class SmsLogRepository
{
    private readonly string _connStr;
    private readonly ILogger _logger;

    public SmsLogRepository(string connectionString, ILogger logger)
    {
        _connStr = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the shared.sms_logs table exists. Called once at startup.
    /// </summary>
    public async Task EnsureTableExistsAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                CREATE SCHEMA IF NOT EXISTS shared;

                CREATE TABLE IF NOT EXISTS shared.sms_logs (
                    id                  UUID PRIMARY KEY,
                    recipient_phone     TEXT NOT NULL,
                    message_type        TEXT NOT NULL,
                    related_entity_type TEXT,
                    related_entity_id   UUID,
                    message             TEXT NOT NULL,
                    provider            TEXT NOT NULL,
                    provider_message_id TEXT,
                    status              TEXT NOT NULL DEFAULT 'Pending',
                    error_message       TEXT,
                    sent_at             TIMESTAMPTZ,
                    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ix_sms_logs_entity
                    ON shared.sms_logs (related_entity_type, related_entity_id, created_at DESC);

                CREATE INDEX IF NOT EXISTS ix_sms_logs_sent_at
                    ON shared.sms_logs (sent_at DESC);
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // Log but don't throw — SMS log table is optional infrastructure
            _logger.LogWarning(ex, "Failed to create shared.sms_logs table (non-critical)");
        }
    }

    /// <summary>
    /// Inserts an SMS log record into the database.
    /// </summary>
    public async Task<Guid> InsertAsync(string recipientPhone, string messageType,
        string? relatedEntityType, Guid? relatedEntityId,
        string message, string provider, string status,
        CancellationToken ct = default)
    {
        var logId = Guid.NewGuid();
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO shared.sms_logs
                    (id, recipient_phone, message_type, related_entity_type, related_entity_id,
                     message, provider, status, created_at)
                VALUES
                    (@id, @recipient_phone, @message_type, @related_entity_type, @related_entity_id,
                     @message, @provider, @status, NOW());
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", logId);
            cmd.Parameters.AddWithValue("recipient_phone", recipientPhone);
            cmd.Parameters.AddWithValue("message_type", messageType);
            cmd.Parameters.AddWithValue("related_entity_type", (object?)relatedEntityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("related_entity_id", (object?)relatedEntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("message", message);
            cmd.Parameters.AddWithValue("provider", provider);
            cmd.Parameters.AddWithValue("status", status);

            await cmd.ExecuteNonQueryAsync(ct);
            return logId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to insert SMS log for {MessageType}", messageType);
            return logId; // Return the ID even if insert failed (log is non-critical)
        }
    }

    /// <summary>
    /// Updates the status of an existing SMS log record after send attempt.
    /// </summary>
    public async Task UpdateSendResultAsync(
        Guid logId, bool success, string? providerMessageId = null,
        string? errorMessage = null, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var status = success ? "Sent" : "Failed";
            const string sql = """
                UPDATE shared.sms_logs
                SET status = @status,
                    provider_message_id = COALESCE(@provider_message_id, provider_message_id),
                    error_message = COALESCE(@error_message, error_message),
                    sent_at = CASE WHEN @status = 'Sent' THEN NOW() ELSE sent_at END
                WHERE id = @id;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", logId);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("provider_message_id", (object?)providerMessageId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("error_message", (object?)errorMessage ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SMS log {LogId}", logId);
        }
    }

    /// <summary>
    /// Checks if a similar SMS was already sent recently (duplicate prevention).
    /// </summary>
    public async Task<bool> WasRecentlySentAsync(
        string entityType, Guid entityId, string messageType,
        int withinSeconds = 300, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var paramSql = """
                SELECT COUNT(*) FROM shared.sms_logs
                WHERE related_entity_type = @entity_type
                  AND related_entity_id = @entity_id
                  AND message_type = @message_type
                  AND status = 'Sent'
                  AND sent_at > NOW() - (@interval || ' seconds')::INTERVAL;
            """;

            await using var cmd = new NpgsqlCommand(paramSql, conn);
            cmd.Parameters.AddWithValue("entity_type", entityType);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            cmd.Parameters.AddWithValue("message_type", messageType);
            cmd.Parameters.AddWithValue("interval", withinSeconds.ToString());

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check duplicate SMS for {EntityType}:{EntityId}",
                entityType, entityId);
            return false; // Fail open for duplicate check
        }
    }
}
