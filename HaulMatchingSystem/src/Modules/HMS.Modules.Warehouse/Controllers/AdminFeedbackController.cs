using System.Security.Claims;
using HMS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

/// <summary>
/// Admin/Warehouse_Staff feedback management controller.
///
/// GET    /api/staff/feedbacks                         — list feedbacks (paged, filtered)
/// GET    /api/staff/feedbacks/{feedbackId}            — feedback detail
/// GET    /api/staff/feedbacks/{feedbackId}/evidence/{evidenceId} — download evidence
/// </summary>
[ApiController]
[Route("api/staff/feedbacks")]
[Authorize(Roles = "Admin,Warehouse_Staff")]
public class AdminFeedbackController : ControllerBase
{
    private readonly string _connStr;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AdminFeedbackController> _logger;

    public AdminFeedbackController(
        IConfiguration config,
        IFileStorageService fileStorage,
        ILogger<AdminFeedbackController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
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

    private string GetRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // ─── GET /api/staff/feedbacks ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ListFeedbacks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? rating = null,
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
                return Ok(new FeedbackPagedResult { Page = page, PageSize = pageSize });
            userHubId = (Guid)hubResult;
        }

        var conditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (rating.HasValue)
        {
            conditions.Add("sf.rating = @rating");
            parameters.Add(new("rating", rating.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(s.shipment_code ILIKE @search OR u_customer.full_name ILIKE @search OR sf.comment ILIKE @search)");
            parameters.Add(new("search", $"%{search}%"));
        }

        // Hub isolation: Warehouse_Staff only sees feedback for shipments from their hub
        if (userHubId.HasValue)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM transport.trip_shipments ts
                    JOIN transport.trips t ON t.id = ts.trip_id AND t.is_deleted = FALSE
                    WHERE ts.shipment_id = sf.shipment_id
                      AND ts.is_deleted = FALSE
                      AND (t.origin_hub_id = @hub_id OR t.dest_hub_id = @hub_id)
                )
            """);
            parameters.Add(new("hub_id", userHubId.Value));
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        // Count
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM warehouse.shipment_feedbacks sf
            JOIN warehouse.shipments s ON s.id = sf.shipment_id
            LEFT JOIN identity.users u_customer ON u_customer.id = sf.customer_id
            {whereClause};
        """;
        int totalCount;
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            foreach (var p in parameters)
                countCmd.Parameters.Add(p.Clone());
            totalCount = (int)(await countCmd.ExecuteScalarAsync(ct) ?? 0);
            if (totalCount == 0)
                return Ok(new FeedbackPagedResult { Page = page, PageSize = pageSize });
        }

        // Fetch
        var offset = (page - 1) * pageSize;
        var fetchSql = $"""
            SELECT sf.id, sf.shipment_id, sf.customer_id, sf.rating, sf.comment,
                   sf.created_at, sf.updated_at,
                   s.shipment_code,
                   u_customer.full_name AS customer_name,
                   (SELECT COUNT(*) FROM warehouse.shipment_feedback_evidence WHERE feedback_id = sf.id) AS evidence_count
            FROM warehouse.shipment_feedbacks sf
            JOIN warehouse.shipments s ON s.id = sf.shipment_id
            LEFT JOIN identity.users u_customer ON u_customer.id = sf.customer_id
            {whereClause}
            ORDER BY sf.created_at DESC
            LIMIT @limit OFFSET @offset;
        """;
        await using var cmd = new NpgsqlCommand(fetchSql, conn);
        foreach (var p in parameters)
            cmd.Parameters.Add(p.Clone());
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        var items = new List<FeedbackListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new FeedbackListItem
            {
                Id = reader.GetGuid(0),
                ShipmentId = reader.GetGuid(1),
                CustomerId = reader.GetGuid(2),
                Rating = reader.GetInt32(3),
                Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                ShipmentCode = reader.IsDBNull(7) ? null : reader.GetString(7),
                CustomerName = reader.IsDBNull(8) ? "Khách hàng" : reader.GetString(8),
                EvidenceCount = reader.GetInt32(9),
            });
        }

        return Ok(new FeedbackPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    // ─── GET /api/staff/feedbacks/{feedbackId} ────────────────────
    [HttpGet("{feedbackId:guid}")]
    public async Task<IActionResult> GetFeedbackDetail(Guid feedbackId, CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT sf.id, sf.shipment_id, sf.customer_id, sf.rating, sf.comment,
                   sf.created_at, sf.updated_at,
                   s.shipment_code, s.status AS shipment_status,
                   u_customer.full_name AS customer_name, u_customer.email AS customer_email
            FROM warehouse.shipment_feedbacks sf
            JOIN warehouse.shipments s ON s.id = sf.shipment_id
            LEFT JOIN identity.users u_customer ON u_customer.id = sf.customer_id
            WHERE sf.id = @feedback_id;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("feedback_id", feedbackId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy phản hồi." });

        var shipmentId = reader.GetGuid(1);
        var customerId = reader.GetGuid(2);
        var ratingVal = reader.GetInt32(3);
        var commentVal = reader.IsDBNull(4) ? null : reader.GetString(4);
        var createdAtVal = reader.GetDateTime(5);
        var updatedAtVal = reader.GetDateTime(6);
        var shipmentCode = reader.IsDBNull(7) ? null : reader.GetString(7);
        var shipmentStatus = reader.IsDBNull(8) ? null : reader.GetString(8);
        var customerName = reader.IsDBNull(9) ? "Khách hàng" : reader.GetString(9);
        var customerEmail = reader.IsDBNull(10) ? null : reader.GetString(10);

        await reader.CloseAsync();

        // Hub isolation for Warehouse_Staff
        if (role == "Warehouse_Staff")
        {
            var hasAccess = await VerifyHubAccessAsync(conn, shipmentId, userId, ct);
            if (!hasAccess)
                return Forbid();
        }

        // Load evidence
        var evidence = await LoadEvidenceAsync(conn, feedbackId, ct);

        return Ok(new
        {
            id = feedbackId,
            shipmentId,
            customerId,
            rating = ratingVal,
            comment = commentVal,
            createdAt = createdAtVal,
            updatedAt = updatedAtVal,
            shipmentCode,
            shipmentStatus,
            customerName,
            customerEmail,
            evidence
        });
    }

    // ─── GET /api/staff/feedbacks/{feedbackId}/evidence/{evidenceId} ──
    [HttpGet("{feedbackId:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> DownloadEvidence(
        Guid feedbackId,
        Guid evidenceId,
        CancellationToken ct)
    {
        var (userId, role) = GetCurrentUser();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Load evidence record + verify ownership
        const string evSql = """
            SELECT sfme.storage_key, sfme.content_type, sfme.original_file_name,
                   sf.shipment_id, sf.customer_id
            FROM warehouse.shipment_feedback_evidence sfme
            JOIN warehouse.shipment_feedbacks sf ON sf.id = sfme.feedback_id
            WHERE sfme.id = @evidence_id AND sfme.feedback_id = @feedback_id;
        """;
        await using var evCmd = new NpgsqlCommand(evSql, conn);
        evCmd.Parameters.AddWithValue("feedback_id", feedbackId);
        evCmd.Parameters.AddWithValue("evidence_id", evidenceId);
        await using var reader = await evCmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy file minh chứng." });

        var storageKey = reader.GetString(0);
        var contentType = reader.IsDBNull(1) ? "application/octet-stream" : reader.GetString(1);
        var fileName = reader.IsDBNull(2) ? "file" : reader.GetString(2);
        var shipmentId = reader.GetGuid(3);
        await reader.CloseAsync();

        // Hub isolation for Warehouse_Staff
        if (role == "Warehouse_Staff")
        {
            var hasAccess = await VerifyHubAccessAsync(conn, shipmentId, userId, ct);
            if (!hasAccess)
                return Forbid();
        }

        // Stream file
        var result = await _fileStorage.OpenReadAsync(storageKey, ct);
        if (result == null)
            return NotFound(new { message = "File không tồn tại trên hệ thống." });

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static async Task<bool> VerifyHubAccessAsync(NpgsqlConnection conn, Guid shipmentId, Guid staffUserId, CancellationToken ct)
    {
        // Get staff's hub
        const string hubSql = "SELECT hub_id FROM identity.users WHERE id = @user_id AND is_deleted = FALSE;";
        await using var hubCmd = new NpgsqlCommand(hubSql, conn);
        hubCmd.Parameters.AddWithValue("user_id", staffUserId);
        var hubResult = await hubCmd.ExecuteScalarAsync(ct);
        if (hubResult == null || hubResult == DBNull.Value)
            return false;

        var hubId = (Guid)hubResult;

        // Check shipment relates to this hub via trips
        const string tripSql = """
            SELECT EXISTS(
                SELECT 1 FROM transport.trip_shipments ts
                JOIN transport.trips t ON t.id = ts.trip_id AND t.is_deleted = FALSE
                WHERE ts.shipment_id = @shipment_id
                  AND ts.is_deleted = FALSE
                  AND (t.origin_hub_id = @hub_id OR t.dest_hub_id = @hub_id)
            );
        """;
        await using var tripCmd = new NpgsqlCommand(tripSql, conn);
        tripCmd.Parameters.AddWithValue("shipment_id", shipmentId);
        tripCmd.Parameters.AddWithValue("hub_id", hubId);
        var result = await tripCmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private async Task<List<object>> LoadEvidenceAsync(NpgsqlConnection conn, Guid feedbackId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, original_file_name, content_type, file_size, uploaded_at
            FROM warehouse.shipment_feedback_evidence
            WHERE feedback_id = @feedback_id
            ORDER BY uploaded_at ASC;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("feedback_id", feedbackId);
        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                id = reader.GetGuid(0),
                originalFileName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                contentType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                fileSize = reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                uploadedAt = reader.GetDateTime(4)
            });
        }
        return items;
    }
}

// ─── Response DTOs ─────────────────────────────────────────────────

public sealed class FeedbackListItem
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid CustomerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ShipmentCode { get; set; }
    public string? CustomerName { get; set; }
    public int EvidenceCount { get; set; }
}

public sealed class FeedbackPagedResult
{
    public List<FeedbackListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
