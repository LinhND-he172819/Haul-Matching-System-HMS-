using System.Security.Claims;
using HMS.Modules.Warehouse.Application.DTOs.Incident;
using HMS.Modules.Warehouse.Application.Services;
using HMS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

/// <summary>
/// Admin / Warehouse_Staff incident management controller.
///
/// GET    /api/staff/incidents                        — list incidents (paged, filtered)
/// GET    /api/staff/incidents/{incidentId}           — incident detail
/// POST   /api/staff/incidents/{incidentId}/take      — Open → InProgress
/// POST   /api/staff/incidents/{incidentId}/resolve   — InProgress → Resolved
/// POST   /api/staff/incidents/{incidentId}/reject    — Open → Rejected
/// GET    /api/staff/incidents/{incidentId}/evidence/{evidenceId} — download evidence
/// </summary>
[ApiController]
[Route("api/staff/incidents")]
[Authorize(Roles = "Admin,Warehouse_Staff")]
public class AdminIncidentController : ControllerBase
{
    private readonly string _connStr;
    private readonly IncidentService _incidentService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AdminIncidentController> _logger;

    public AdminIncidentController(
        IConfiguration config,
        IncidentService incidentService,
        IFileStorageService fileStorage,
        ILogger<AdminIncidentController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _incidentService = incidentService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private (Guid UserId, string Role) GetCurrentUser()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        return (userId, role);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        return userId;
    }

    private string GetRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "";
    }

    // ─── GET /api/staff/incidents ─────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ListIncidents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? incidentType = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation for Warehouse_Staff
        Guid? userHubId = null;
        if (role == "Warehouse_Staff")
        {
            const string hubSql = "SELECT hub_id FROM identity.users WHERE id = @user_id AND is_deleted = FALSE;";
            await using var hubCmd = new NpgsqlCommand(hubSql, conn);
            hubCmd.Parameters.AddWithValue("user_id", userId);
            var hubResult = await hubCmd.ExecuteScalarAsync(ct);
            if (hubResult == null || hubResult == DBNull.Value)
                return Ok(new IncidentPagedResult());
            userHubId = (Guid)hubResult;
        }

        var conditions = new List<string> { "ti.is_deleted = FALSE" };
        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("ti.status = @status");
            parameters.Add(new("status", status));
        }

        if (!string.IsNullOrWhiteSpace(incidentType))
        {
            conditions.Add("ti.incident_type = @incident_type");
            parameters.Add(new("incident_type", incidentType));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(ti.description ILIKE @search OR ti.incident_code ILIKE @search OR u_driver.full_name ILIKE @search)");
            parameters.Add(new("search", $"%{search}%"));
        }

        // Hub isolation: only show incidents from trips related to user's hub
        if (userHubId.HasValue)
        {
            conditions.Add("(t.origin_hub_id = @hub_id OR t.dest_hub_id = @hub_id)");
            parameters.Add(new("hub_id", userHubId.Value));
        }

        var whereClause = string.Join(" AND ", conditions);

        // Count first
        int totalCount;
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM transport.trip_incidents ti
            JOIN transport.trips t ON t.id = ti.trip_id
            JOIN identity.users u_driver ON u_driver.id = ti.reported_by
            LEFT JOIN identity.users u_assigned ON u_assigned.id = ti.assigned_to_user_id
            WHERE {whereClause};
        """;
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            foreach (var p in parameters)
                countCmd.Parameters.Add(p.Clone());
            totalCount = (int)(await countCmd.ExecuteScalarAsync(ct))!;
            if (totalCount == 0)
                return Ok(new IncidentPagedResult { Page = page, PageSize = pageSize });
        }

        // Fetch
        var offset = (page - 1) * pageSize;
        var fetchSql = $"""
            SELECT ti.id, ti.incident_code, ti.incident_type, ti.description, ti.status,
                   ti.trip_id, t.trip_code,
                   u_driver.full_name AS driver_name, ti.reported_by,
                   v.license_plate,
                   CONCAT(oh.name, ' → ', dh.name) AS route,
                   ti.created_at,
                   (SELECT COUNT(*) FROM transport.trip_incident_evidence WHERE incident_id = ti.id) AS evidence_count,
                   u_assigned.full_name AS assigned_to_name, ti.assigned_to_user_id
            FROM transport.trip_incidents ti
            JOIN transport.trips t ON t.id = ti.trip_id
            JOIN identity.users u_driver ON u_driver.id = ti.reported_by
            LEFT JOIN identity.users u_assigned ON u_assigned.id = ti.assigned_to_user_id
            LEFT JOIN transport.vehicles v ON v.id = t.vehicle_id
            LEFT JOIN identity.hubs oh ON oh.id = t.origin_hub_id
            LEFT JOIN identity.hubs dh ON dh.id = t.dest_hub_id
            WHERE {whereClause}
            ORDER BY ti.created_at DESC
            LIMIT @limit OFFSET @offset;
        """;
        await using var cmd = new NpgsqlCommand(fetchSql, conn);
        foreach (var p in parameters)
            cmd.Parameters.Add(p.Clone());
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        var items = new List<IncidentListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new IncidentListItem
            {
                Id = reader.GetGuid(0),
                IncidentCode = reader.IsDBNull(1) ? $"INC-{reader.GetGuid(0).ToString()[..8]}" : reader.GetString(1),
                IncidentType = reader.GetString(2),
                Description = reader.GetString(3),
                Status = reader.GetString(4),
                TripId = reader.GetGuid(5).ToString(),
                TripCode = reader.IsDBNull(6) ? $"TRIP-{reader.GetGuid(5).ToString()[..8]}" : reader.GetString(6),
                DriverName = reader.GetString(7),
                DriverId = reader.GetGuid(8).ToString(),
                VehiclePlate = reader.IsDBNull(9) ? null : reader.GetString(9),
                Route = reader.IsDBNull(10) ? null : reader.GetString(10),
                ReportedAt = reader.GetDateTime(11),
                EvidenceCount = reader.GetInt32(12),
                AssignedToName = reader.IsDBNull(13) ? null : reader.GetString(13),
                AssignedToId = reader.IsDBNull(14) ? null : reader.GetGuid(14).ToString(),
            });
        }

        var countResult = new IncidentPagedResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(countResult);
    }

    private async Task<int> GetCountAsync(NpgsqlConnection conn, string whereClause, List<NpgsqlParameter> parameters, CancellationToken ct)
    {
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM transport.trip_incidents ti
            JOIN transport.trips t ON t.id = ti.trip_id
            JOIN identity.users u_driver ON u_driver.id = ti.reported_by
            WHERE {whereClause};
        """;
        await using var cmd = new NpgsqlCommand(countSql, conn);
        foreach (var p in parameters)
            cmd.Parameters.Add(p.Clone());
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    // ─── GET /api/staff/incidents/{incidentId} ────────────────────────
    [HttpGet("{incidentId:guid}")]
    public async Task<IActionResult> GetIncidentDetail(Guid incidentId, CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation check
        if (!await _incidentService.CanUserAccessIncidentAsync(conn, userId, role, incidentId, ct))
            return NotFound(new { message = "Không tìm thấy sự cố hoặc bạn không có quyền truy cập." });

        var info = await _incidentService.GetIncidentInfoAsync(conn, incidentId, ct);

        // Load evidence
        const string evidenceSql = """
            SELECT id, original_file_name, content_type, file_size, uploaded_at
            FROM transport.trip_incident_evidence
            WHERE incident_id = @incident_id
            ORDER BY uploaded_at ASC;
        """;
        await using var evCmd = new NpgsqlCommand(evidenceSql, conn);
        evCmd.Parameters.AddWithValue("incident_id", incidentId);
        var evidence = new List<IncidentEvidenceDto>();
        await using var evReader = await evCmd.ExecuteReaderAsync(ct);
        while (await evReader.ReadAsync(ct))
        {
            evidence.Add(new IncidentEvidenceDto
            {
                Id = evReader.GetGuid(0),
                OriginalFileName = evReader.IsDBNull(1) ? "unknown" : evReader.GetString(1),
                ContentType = evReader.IsDBNull(2) ? "image/jpeg" : evReader.GetString(2),
                FileSize = evReader.IsDBNull(3) ? 0 : evReader.GetInt64(3),
                UploadedAt = evReader.GetDateTime(4)
            });
        }
        await evReader.CloseAsync();

        // Compute allowed actions — Admin can take/resolve/reject; Warehouse_Staff can take/resolve
        var allowedActions = new IncidentAllowedActions
        {
            CanTake = info.Status == "Open" && (role == "Admin" || role == "Warehouse_Staff"),
            CanResolve = info.Status == "InProgress" && (role == "Admin" || role == "Warehouse_Staff"),
            CanReject = info.Status == "Open" && role == "Admin"
        };

        return Ok(new IncidentDetailDto
        {
            Id = incidentId,
            IncidentCode = info.IncidentCode,
            IncidentType = info.IncidentType,
            Description = info.Description,
            Status = info.Status,
            TripId = info.TripId.ToString(),
            TripCode = info.TripCode,
            DriverId = info.ReportedBy,
            DriverName = info.DriverName,
            VehiclePlate = info.Vehicle,
            Route = info.Route,
            ReportedAt = info.ReportedAt,
            UpdatedAt = info.UpdatedAt,
            AssignedToUserId = info.AssignedToUserId,
            AssignedToName = info.AssignedToName,
            AssignedAt = info.AssignedAt,
            ResolutionNote = info.ResolutionNote,
            ResolvedByUserId = info.ResolvedByUserId,
            ResolvedByName = info.ResolvedBy,
            ResolvedAt = info.ResolvedAt,
            Evidence = evidence,
            AllowedActions = new List<IncidentAllowedActions> { allowedActions }
        });
    }

    // ─── POST /api/staff/incidents/{incidentId}/take ───────────────────
    [HttpPost("{incidentId:guid}/take")]
    public async Task<IActionResult> TakeIncident(Guid incidentId, [FromBody] IncidentStatusChangeRequest? request, CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation check
        if (!await _incidentService.CanUserAccessIncidentAsync(conn, userId, role, incidentId, ct))
            return NotFound(new { message = "Không tìm thấy sự cố." });

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Get current status
            const string statusSql = "SELECT status FROM transport.trip_incidents WHERE id = @id;";
            await using (var cmd = new NpgsqlCommand(statusSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                var currentStatus = (string)(await cmd.ExecuteScalarAsync(ct))!;
                IncidentService.EnsureValidTransition(currentStatus, "InProgress");
            }

            // Update: set InProgress + assigned_to + assigned_at + updated_at
            var now = DateTimeOffset.UtcNow;
            const string updateSql = """
                UPDATE transport.trip_incidents
                SET status = 'InProgress',
                    assigned_to_user_id = @user_id,
                    assigned_at = @now,
                    updated_at = @now
                WHERE id = @id;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("now", now);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('TripIncident', @entity_id, 'IncidentTaken', @user_id, to_jsonb(@details::text), @now);
            """;
            await using (var auditCmd = new NpgsqlCommand(auditSql, conn, tx))
            {
                auditCmd.Parameters.AddWithValue("entity_id", incidentId);
                auditCmd.Parameters.AddWithValue("user_id", userId);
                auditCmd.Parameters.AddWithValue("now", now);
                auditCmd.Parameters.AddWithValue("details", (object?)request?.Note ?? DBNull.Value);
                await auditCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            // Send email (with result tracking)
            var emailSent = false;
            var emailError = (string?)null;
            try
            {
                using var emailConn = new NpgsqlConnection(_connStr);
                await emailConn.OpenAsync(ct);
                var recipients = await _incidentService.GetIncidentRecipientsAsync(emailConn, incidentId, "InProgress", ct);
                if (recipients.Count > 0)
                {
                    await _incidentService.SendIncidentInProgressEmailAsync(emailConn, incidentId, userId, ct);
                    emailSent = true;
                    _logger.LogInformation("IncidentInProgress email sent to {Count} recipients for {IncidentId}", recipients.Count, incidentId);
                }
                else
                {
                    emailError = "Không tìm thấy người nhận email";
                    _logger.LogWarning("No email recipients found for IncidentInProgress {IncidentId}", incidentId);
                }
            }
            catch (Exception ex)
            {
                emailError = ex.Message;
                _logger.LogError(ex, "Failed to send IncidentInProgress email for {IncidentId}", incidentId);
            }

            return Ok(new { message = "Đã tiếp nhận sự cố. Trạng thái: InProgress.", status = "InProgress", emailSent, emailError });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ─── POST /api/staff/incidents/{incidentId}/resolve ────────────────
    [HttpPost("{incidentId:guid}/resolve")]
    public async Task<IActionResult> ResolveIncident(
        Guid incidentId,
        [FromBody] IncidentStatusChangeRequest request,
        CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation check
        if (!await _incidentService.CanUserAccessIncidentAsync(conn, userId, role, incidentId, ct))
            return NotFound(new { message = "Không tìm thấy sự cố." });

        if (request is null || string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Vui lòng nhập nội dung xử lý." });

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Get current status
            const string statusSql = "SELECT status FROM transport.trip_incidents WHERE id = @id;";
            await using (var cmd = new NpgsqlCommand(statusSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                var currentStatus = (string)(await cmd.ExecuteScalarAsync(ct))!;
                IncidentService.EnsureValidTransition(currentStatus, "Resolved");
            }

            // Update: set Resolved
            var now = DateTimeOffset.UtcNow;
            const string updateSql = """
                UPDATE transport.trip_incidents
                SET status = 'Resolved',
                    resolution_note = @note,
                    resolved_by_user_id = @user_id,
                    resolved_at = @now,
                    updated_at = @now
                WHERE id = @id;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                cmd.Parameters.AddWithValue("note", request.Note);
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("now", now);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('TripIncident', @entity_id, 'IncidentResolved', @user_id, to_jsonb(@details::text), @now);
            """;
            await using (var auditCmd = new NpgsqlCommand(auditSql, conn, tx))
            {
                auditCmd.Parameters.AddWithValue("entity_id", incidentId);
                auditCmd.Parameters.AddWithValue("user_id", userId);
                auditCmd.Parameters.AddWithValue("now", now);
                auditCmd.Parameters.AddWithValue("details", request.Note ?? (object)DBNull.Value);
                await auditCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            // Send email (with result tracking)
            var emailSent = false;
            var emailError = (string?)null;
            try
            {
                using var emailConn = new NpgsqlConnection(_connStr);
                await emailConn.OpenAsync(ct);
                var recipients = await _incidentService.GetIncidentRecipientsAsync(emailConn, incidentId, "Resolved", ct);
                if (recipients.Count > 0)
                {
                    await _incidentService.SendIncidentResolvedEmailAsync(emailConn, incidentId, userId, request.Note, now, ct);
                    emailSent = true;
                    _logger.LogInformation("IncidentResolved email sent to {Count} recipients for {IncidentId}", recipients.Count, incidentId);
                }
                else
                {
                    emailError = "Không tìm thấy người nhận email";
                    _logger.LogWarning("No email recipients found for IncidentResolved {IncidentId}", incidentId);
                }
            }
            catch (Exception ex)
            {
                emailError = ex.Message;
                _logger.LogError(ex, "Failed to send IncidentResolved email for {IncidentId}", incidentId);
            }

            return Ok(new { message = "Đã xử lý xong sự cố.", status = "Resolved", note = request.Note, emailSent, emailError });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ─── POST /api/staff/incidents/{incidentId}/reject ─────────────────
    [HttpPost("{incidentId:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectIncident(
        Guid incidentId,
        [FromBody] IncidentStatusChangeRequest request,
        CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        if (request is null || string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Get current status
            const string statusSql = "SELECT status FROM transport.trip_incidents WHERE id = @id;";
            await using (var cmd = new NpgsqlCommand(statusSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                var currentStatus = (string)(await cmd.ExecuteScalarAsync(ct))!;
                IncidentService.EnsureValidTransition(currentStatus, "Rejected");
            }

            // Update: set Rejected
            var now = DateTimeOffset.UtcNow;
            const string updateSql = """
                UPDATE transport.trip_incidents
                SET status = 'Rejected',
                    resolution_note = @note,
                    resolved_by_user_id = @user_id,
                    resolved_at = @now,
                    updated_at = @now
                WHERE id = @id;
            """;
            await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", incidentId);
                cmd.Parameters.AddWithValue("note", request.Note);
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("now", now);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Audit
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('TripIncident', @entity_id, 'IncidentRejected', @user_id, to_jsonb(@details::text), @now);
            """;
            await using (var auditCmd = new NpgsqlCommand(auditSql, conn, tx))
            {
                auditCmd.Parameters.AddWithValue("entity_id", incidentId);
                auditCmd.Parameters.AddWithValue("user_id", userId);
                auditCmd.Parameters.AddWithValue("now", now);
                auditCmd.Parameters.AddWithValue("details", request.Note ?? (object)DBNull.Value);
                await auditCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            // Send email (with result tracking)
            var emailSent = false;
            var emailError = (string?)null;
            try
            {
                using var emailConn = new NpgsqlConnection(_connStr);
                await emailConn.OpenAsync(ct);
                var recipients = await _incidentService.GetIncidentRecipientsAsync(emailConn, incidentId, "Rejected", ct);
                if (recipients.Count > 0)
                {
                    await _incidentService.SendIncidentRejectedEmailAsync(emailConn, incidentId, userId, request.Note, now, ct);
                    emailSent = true;
                    _logger.LogInformation("IncidentRejected email sent to {Count} recipients for {IncidentId}", recipients.Count, incidentId);
                }
                else
                {
                    emailError = "Không tìm thấy người nhận email";
                    _logger.LogWarning("No email recipients found for IncidentRejected {IncidentId}", incidentId);
                }
            }
            catch (Exception ex)
            {
                emailError = ex.Message;
                _logger.LogError(ex, "Failed to send IncidentRejected email for {IncidentId}", incidentId);
            }

            return Ok(new { message = "Đã từ chối báo cáo sự cố.", status = "Rejected", reason = request.Note, emailSent, emailError });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ─── GET /api/staff/incidents/{incidentId}/history ────────────────
    [HttpGet("{incidentId:guid}/history")]
    public async Task<IActionResult> GetIncidentHistory(Guid incidentId, CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation check
        if (!await _incidentService.CanUserAccessIncidentAsync(conn, userId, role, incidentId, ct))
            return NotFound(new { message = "Không tìm thấy sự cố hoặc bạn không có quyền truy cập." });

        // Fetch audit log entries for this incident
        const string sql = """
            SELECT al.id, al.action, al.performed_by, al.details, al.created_at,
                   u.full_name AS actor_name
            FROM shared.audit_log al
            LEFT JOIN identity.users u ON u.id = al.performed_by
            WHERE al.entity_type = 'TripIncident' AND al.entity_id = @incident_id
            ORDER BY al.created_at ASC;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("incident_id", incidentId);

        var items = new List<IncidentHistoryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new IncidentHistoryItem
            {
                Id = reader.GetGuid(0),
                Action = reader.GetString(1),
                ActorUserId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                ActorName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                Note = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return Ok(new { items });
    }

    // ─── GET /api/staff/incidents/{incidentId}/evidence/{evidenceId} ──
    [HttpGet("{incidentId:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> DownloadEvidence(Guid incidentId, Guid evidenceId, CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Hub isolation check
        if (!await _incidentService.CanUserAccessIncidentAsync(conn, userId, role, incidentId, ct))
            return Forbid();

        const string sql = """
            SELECT storage_key, original_file_name, content_type
            FROM transport.trip_incident_evidence
            WHERE id = @evidence_id AND incident_id = @incident_id;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("evidence_id", evidenceId);
        cmd.Parameters.AddWithValue("incident_id", incidentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy file." });

        var storageKey = reader.GetString(0);
        var fileName = reader.IsDBNull(1) ? "file" : reader.GetString(1);
        var contentType = reader.IsDBNull(2) ? "application/octet-stream" : reader.GetString(2);

        try
        {
            var result = await _fileStorage.OpenReadAsync(storageKey, ct);
            if (result is null)
                return NotFound(new { message = "File không tồn tại trên hệ thống." });
            return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "File không tồn tại trên hệ thống." });
        }
    }
}
