using System.Data;
using System.Security.Claims;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Application.Services;

/// <summary>
/// Core incident business logic shared between Driver and Admin/Staff controllers.
/// Handles: state transitions, hub isolation, recipient resolution, email notifications.
/// Uses raw Npgsql SQL following the project's existing Warehouse module pattern.
/// </summary>
public sealed class IncidentService
{
    private readonly string _connStr;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<IncidentService> _logger;

    // Incident state machine: allowed transitions
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        ["Open"] = new() { "InProgress", "Rejected" },
        ["InProgress"] = new() { "Resolved" },
    };

    public IncidentService(
        IConfiguration configuration,
        IEmailService emailService,
        IEmailTemplateService templateService,
        ILogger<IncidentService> logger)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection") ?? "";
        _emailService = emailService;
        _templateService = templateService;
        _logger = logger;
    }

    // ─── State Machine ───────────────────────────────────────────────

    public static void EnsureValidTransition(string fromStatus, string toStatus)
    {
        if (!AllowedTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(toStatus))
        {
            throw new InvalidOperationException(
                $"Chuyển trạng thái không hợp lệ: {fromStatus} → {toStatus}. " +
                $"Chỉ được phép: {string.Join(", ", AllowedTransitions.SelectMany(kv => kv.Value.Select(v => $"{kv.Key}→{v}")))}.");
        }
    }

    // ─── Hub Isolation ──────────────────────────────────────────────

    /// <summary>
    /// Checks whether a user can access an incident based on their role and hub.
    /// Admin: always allowed.
    /// Warehouse_Staff: only if the incident's trip is related to their hub.
    /// Returns true if access is allowed.
    /// </summary>
    public async Task<bool> CanUserAccessIncidentAsync(NpgsqlConnection conn, Guid userId, string role, Guid incidentId, CancellationToken ct)
    {
        if (role == "Admin")
            return true;

        if (role != "Warehouse_Staff")
            return false;

        // Get user's hub_id
        const string hubSql = "SELECT hub_id FROM identity.users WHERE id = @user_id AND is_deleted = FALSE;";
        await using (var cmd = new NpgsqlCommand(hubSql, conn))
        {
            cmd.Parameters.AddWithValue("user_id", userId);
            var hubResult = await cmd.ExecuteScalarAsync(ct);
            if (hubResult == null || hubResult == DBNull.Value)
                return false;

            var userHubId = (Guid)hubResult;

            // Check if the incident's trip is related to user's hub
            // Trip is related if origin_hub_id or dest_hub_id matches
            const string tripHubSql = """
                SELECT t.origin_hub_id, t.dest_hub_id
                FROM transport.trip_incidents ti
                JOIN transport.trips t ON t.id = ti.trip_id
                WHERE ti.id = @incident_id;
            """;
            await using var cmd2 = new NpgsqlCommand(tripHubSql, conn);
            cmd2.Parameters.AddWithValue("incident_id", incidentId);
            await using var reader = await cmd2.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return false;

            var originHubId = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0);
            var destHubId = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1);

            return userHubId == originHubId || userHubId == destHubId;
        }
    }

    // ─── Email Recipients ────────────────────────────────────────────

    /// <summary>
    /// Resolves email recipients for incident-related events.
    /// </summary>
    public async Task<List<string>> GetIncidentRecipientsAsync(
        NpgsqlConnection conn, Guid incidentId, string eventType, CancellationToken ct)
    {
        var recipients = new List<string>();

        if (eventType is "Reported" or "InProgress" or "Resolved" or "Rejected")
        {
            // Get incident + trip info
            const string sql = """
                SELECT ti.reported_by, ti.trip_id, t.origin_hub_id, t.dest_hub_id
                FROM transport.trip_incidents ti
                JOIN transport.trips t ON t.id = ti.trip_id
                WHERE ti.id = @incident_id;
            """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("incident_id", incidentId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return recipients;

            var reportedBy = reader.GetGuid(0);
            var tripId = reader.GetGuid(1);
            var originHubId = reader.IsDBNull(2) ? Guid.Empty : reader.GetGuid(2);
            var destHubId = reader.IsDBNull(3) ? Guid.Empty : reader.GetGuid(3);
            await reader.CloseAsync();

            if (eventType == "Reported")
            {
                // Send to: Admin + Warehouse_Staff of related hubs
                var hubIds = new List<Guid>();
                if (originHubId != Guid.Empty) hubIds.Add(originHubId);
                if (destHubId != Guid.Empty) hubIds.Add(destHubId);

                // Get admin emails
                const string adminSql = """
                    SELECT email FROM identity.users
                    WHERE role = 'Admin' AND is_deleted = FALSE AND is_active = TRUE AND email IS NOT NULL AND email != '';
                """;
                await using var adminCmd = new NpgsqlCommand(adminSql, conn);
                await using var adminReader = await adminCmd.ExecuteReaderAsync(ct);
                while (await adminReader.ReadAsync(ct))
                    recipients.Add(adminReader.GetString(0));
                await adminReader.CloseAsync();

                // Get warehouse staff emails for related hubs
                if (hubIds.Count > 0)
                {
                    var placeholders = string.Join(",", hubIds.Select((_, i) => $"@hub{i}"));
                    var staffSql = $"""
                        SELECT email FROM identity.users
                        WHERE role = 'Warehouse_Staff' AND hub_id IN ({placeholders})
                          AND is_deleted = FALSE AND is_active = TRUE AND email IS NOT NULL AND email != '';
                    """;
                    await using var staffCmd = new NpgsqlCommand(staffSql, conn);
                    for (int i = 0; i < hubIds.Count; i++)
                        staffCmd.Parameters.AddWithValue($"@hub{i}", hubIds[i]);
                    await using var staffReader = await staffCmd.ExecuteReaderAsync(ct);
                    while (await staffReader.ReadAsync(ct))
                        recipients.Add(staffReader.GetString(0));
                    await staffReader.CloseAsync();
                }
            }
            else
            {
                // InProgress/Resolved/Rejected → send to driver who reported
                const string driverSql = """
                    SELECT email FROM identity.users WHERE id = @user_id AND is_deleted = FALSE AND email IS NOT NULL AND email != '';
                """;
                await using var driverCmd = new NpgsqlCommand(driverSql, conn);
                driverCmd.Parameters.AddWithValue("user_id", reportedBy);
                var email = await driverCmd.ExecuteScalarAsync(ct);
                if (email != null && email != DBNull.Value)
                    recipients.Add((string)email);
            }
        }

        // Deduplicate (case-insensitive)
        return recipients
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    // ─── Incident Info Retrieval ─────────────────────────────────────

    public async Task<(string IncidentCode, string TripCode, string DriverName, string Vehicle, string Route,
        string IncidentType, string Description, int EvidenceCount, DateTimeOffset ReportedAt,
        string Status, string? AssignedToName, Guid? AssignedToUserId, DateTimeOffset? AssignedAt,
        string? ResolutionNote, string? ResolvedBy, Guid? ResolvedByUserId, DateTimeOffset? ResolvedAt,
        DateTimeOffset UpdatedAt, Guid ReportedBy, Guid TripId)>
        GetIncidentInfoAsync(NpgsqlConnection conn, Guid incidentId, CancellationToken ct)
    {
        const string sql = """
            SELECT ti.incident_code, ti.incident_type, ti.description, ti.status,
                   ti.created_at, ti.reported_by,
                   ti.resolution_note, ti.resolved_by_user_id, ti.resolved_at,
                   ti.trip_id,
                   t.trip_code,
                   u_driver.full_name AS driver_name,
                   v.license_plate,
                   oh.name AS origin_name,
                   dh.name AS dest_name,
                   (SELECT COUNT(*) FROM transport.trip_incident_evidence WHERE incident_id = ti.id) AS evidence_count,
                   ti.assigned_to_user_id, ti.assigned_at, ti.updated_at
            FROM transport.trip_incidents ti
            JOIN transport.trips t ON t.id = ti.trip_id
            JOIN identity.users u_driver ON u_driver.id = ti.reported_by
            LEFT JOIN transport.vehicles v ON v.id = t.vehicle_id
            LEFT JOIN identity.hubs oh ON oh.id = t.origin_hub_id
            LEFT JOIN identity.hubs dh ON dh.id = t.dest_hub_id
            WHERE ti.id = @incident_id;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("incident_id", incidentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new KeyNotFoundException("Không tìm thấy sự cố.");

        var incidentCode = reader.IsDBNull(0) ? $"INC-{incidentId.ToString()[..8]}" : reader.GetString(0);
        var incidentType = reader.GetString(1);
        var description = reader.GetString(2);
        var status = reader.GetString(3);
        var createdAt = reader.GetDateTime(4);
        var reportedBy = reader.GetGuid(5);
        var resolutionNote = reader.IsDBNull(6) ? null : reader.GetString(6);
        var resolvedByUserId = reader.IsDBNull(7) ? (Guid?)null : reader.GetGuid(7);
        var resolvedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : reader.GetDateTime(8);
        var tripId = reader.GetGuid(9);
        var tripCode = reader.IsDBNull(10) ? $"TRIP-{tripId.ToString()[..8]}" : reader.GetString(10);
        var driverName = reader.GetString(11);
        var vehiclePlate = reader.IsDBNull(12) ? "-" : reader.GetString(12);
        var originName = reader.IsDBNull(13) ? "-" : reader.GetString(13);
        var destName = reader.IsDBNull(14) ? "-" : reader.GetString(14);
        var evidenceCount = reader.GetInt32(15);
        var assignedToUserId = reader.IsDBNull(16) ? (Guid?)null : reader.GetGuid(16);
        var assignedAt = reader.IsDBNull(17) ? (DateTimeOffset?)null : reader.GetDateTime(17);
        var updatedAt = reader.IsDBNull(18) ? createdAt : reader.GetDateTime(18);
        await reader.CloseAsync();

        // Resolve assigned_to name
        string? assignedToName = null;
        if (assignedToUserId.HasValue)
        {
            const string assignedNameSql = "SELECT full_name FROM identity.users WHERE id = @uid;";
            await using var aCmd = new NpgsqlCommand(assignedNameSql, conn);
            aCmd.Parameters.AddWithValue("uid", assignedToUserId.Value);
            var nameResult = await aCmd.ExecuteScalarAsync(ct);
            if (nameResult != null) assignedToName = (string)nameResult;
        }

        // Resolve resolved_by name
        string? resolvedByName = null;
        if (resolvedByUserId.HasValue)
        {
            const string resolvedByNameSql = "SELECT full_name FROM identity.users WHERE id = @uid;";
            await using var rCmd = new NpgsqlCommand(resolvedByNameSql, conn);
            rCmd.Parameters.AddWithValue("uid", resolvedByUserId.Value);
            var nameResult = await rCmd.ExecuteScalarAsync(ct);
            if (nameResult != null) resolvedByName = (string)nameResult;
        }

        return (incidentCode, tripCode, driverName, vehiclePlate,
            $"{originName} → {destName}", incidentType, description, evidenceCount,
            createdAt, status, assignedToName, assignedToUserId, assignedAt,
            resolutionNote, resolvedByName, resolvedByUserId, resolvedAt,
            updatedAt, reportedBy, tripId);
    }

    // ─── Email Dispatch ──────────────────────────────────────────────

    public async Task SendIncidentReportedEmailAsync(NpgsqlConnection conn, Guid incidentId, CancellationToken ct)
    {
        try
        {
            var info = await GetIncidentInfoAsync(conn, incidentId, ct);
            var recipients = await GetIncidentRecipientsAsync(conn, incidentId, "Reported", ct);
            if (recipients.Count == 0) return;

            var subject = $"[HMS] Sự cố mới - {info.TripCode} - {MapIncidentType(info.IncidentType)}";
            var body = _templateService.IncidentReported(
                info.IncidentCode, info.TripCode, info.DriverName, info.Vehicle,
                info.Route, info.IncidentType, info.Description, info.EvidenceCount, info.ReportedAt);

            await _emailService.SendToManyAsync(recipients, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send IncidentReported email for {IncidentId}", incidentId);
        }
    }

    public async Task SendIncidentInProgressEmailAsync(NpgsqlConnection conn, Guid incidentId, Guid staffUserId, CancellationToken ct)
    {
        try
        {
            var info = await GetIncidentInfoAsync(conn, incidentId, ct);
            var recipients = await GetIncidentRecipientsAsync(conn, incidentId, "InProgress", ct);
            if (recipients.Count == 0) return;

            // Get staff name
            var staffName = await GetUserFullNameAsync(conn, staffUserId, ct);

            var subject = $"[HMS] Sự cố {info.IncidentCode} đang được xử lý";
            var body = _templateService.IncidentInProgress(
                info.IncidentCode, info.TripCode, info.IncidentType, staffName, DateTimeOffset.UtcNow);

            await _emailService.SendToManyAsync(recipients, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send IncidentInProgress email for {IncidentId}", incidentId);
        }
    }

    public async Task SendIncidentResolvedEmailAsync(NpgsqlConnection conn, Guid incidentId, Guid resolverUserId,
        string resolutionNote, DateTimeOffset resolvedAt, CancellationToken ct)
    {
        try
        {
            var info = await GetIncidentInfoAsync(conn, incidentId, ct);
            var recipients = await GetIncidentRecipientsAsync(conn, incidentId, "Resolved", ct);
            if (recipients.Count == 0) return;

            var resolverName = await GetUserFullNameAsync(conn, resolverUserId, ct);

            var subject = $"[HMS] Sự cố {info.IncidentCode} đã được xử lý";
            var body = _templateService.IncidentResolved(
                info.IncidentCode, info.TripCode, info.IncidentType, resolutionNote, resolverName, resolvedAt);

            await _emailService.SendToManyAsync(recipients, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send IncidentResolved email for {IncidentId}", incidentId);
        }
    }

    public async Task SendIncidentRejectedEmailAsync(NpgsqlConnection conn, Guid incidentId, Guid actorUserId,
        string rejectReason, DateTimeOffset timestamp, CancellationToken ct)
    {
        try
        {
            var info = await GetIncidentInfoAsync(conn, incidentId, ct);
            var recipients = await GetIncidentRecipientsAsync(conn, incidentId, "Rejected", ct);
            if (recipients.Count == 0) return;

            var actorName = await GetUserFullNameAsync(conn, actorUserId, ct);

            var subject = $"[HMS] Báo cáo sự cố {info.IncidentCode} không được tiếp nhận";
            var body = _templateService.IncidentRejected(
                info.IncidentCode, info.TripCode, info.IncidentType, rejectReason, actorName, timestamp);

            await _emailService.SendToManyAsync(recipients, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send IncidentRejected email for {IncidentId}", incidentId);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task<string> GetUserFullNameAsync(NpgsqlConnection conn, Guid userId, CancellationToken ct)
    {
        const string sql = "SELECT full_name FROM identity.users WHERE id = @user_id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString() ?? "N/A";
    }

    private static string MapIncidentType(string type) => type switch
    {
        "Delay" => "Trễ hạn",
        "VehicleBreakdown" => "Hỏng xe",
        "Accident" => "Tai nạn",
        "CargoDamage" => "Hư hỏng hàng hóa",
        "CargoLost" => "Mất hàng",
        "DeliveryProblem" => "Sự cố giao hàng",
        "RouteProblem" => "Sự cố tuyến đường",
        "Weather" => "Thời tiết",
        "Other" => "Khác",
        _ => type
    };
}
