using System.IO;
using System.Security.Claims;
using HMS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

/// <summary>
/// Customer feedback controller.
///
/// POST   /api/customer/shipments/{shipmentId}/feedback          — create feedback (rating + comment)
/// POST   /api/customer/feedbacks/{feedbackId}/evidence          — upload evidence images
/// GET    /api/customer/shipments/{shipmentId}/feedback          — get feedback for shipment
/// </summary>
[ApiController]
[Route("api/customer")]
[Authorize(Roles = "Customer")]
public class CustomerFeedbackController : ControllerBase
{
    private readonly string _connStr;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<CustomerFeedbackController> _logger;

    public CustomerFeedbackController(
        IConfiguration config,
        IFileStorageService fileStorage,
        ILogger<CustomerFeedbackController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        return userId;
    }

    // ─── POST /api/customer/shipments/{shipmentId}/feedback ─────────
    /// <summary>
    /// Create a feedback for a completed shipment.
    /// Body: { "rating": 5, "comment": "..." }
    /// </summary>
    [HttpPost("shipments/{shipmentId:guid}/feedback")]
    public async Task<IActionResult> CreateFeedback(
        Guid shipmentId,
        [FromBody] CreateFeedbackRequest request,
        CancellationToken ct)
    {
        var customerId = GetCurrentUserId();

        // Validate rating
        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { message = "Đánh giá phải từ 1 đến 5 sao." });

        // Validate comment length
        if (!string.IsNullOrEmpty(request.Comment) && request.Comment.Trim().Length > 2000)
            return BadRequest(new { message = "Nội dung nhận xét không được vượt quá 2000 ký tự." });

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // 1. Verify shipment exists and belongs to customer
        string shipmentStatus;
        const string checkSql = """
            SELECT status FROM warehouse.shipments
            WHERE id = @shipment_id AND customer_id = @customer_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập." });
            shipmentStatus = result.ToString()!;
        }

        // 2. Check shipment is eligible for feedback (only Completed status)
        if (shipmentStatus != "Completed")
            return BadRequest(new { message = $"Chỉ có thể đánh giá đơn hàng đã hoàn tất. Trạng thái hiện tại: {shipmentStatus}." });

        // 3. Check duplicate feedback
        const string dupCheck = """
            SELECT id FROM warehouse.shipment_feedbacks
            WHERE shipment_id = @shipment_id AND customer_id = @customer_id;
        """;
        await using (var dupCmd = new NpgsqlCommand(dupCheck, conn))
        {
            dupCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            dupCmd.Parameters.AddWithValue("customer_id", customerId);
            var existing = await dupCmd.ExecuteScalarAsync(ct);
            if (existing != null)
                return Conflict(new { message = "Bạn đã đánh giá đơn hàng này rồi." });
        }

        // 4. Insert feedback
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        const string insertSql = """
            INSERT INTO warehouse.shipment_feedbacks (shipment_id, customer_id, rating, comment, created_at, updated_at)
            VALUES (@shipment_id, @customer_id, @rating, @comment, NOW(), NOW())
            RETURNING id, created_at;
        """;
        Guid feedbackId;
        DateTime createdAt;
        await using (var cmd = new NpgsqlCommand(insertSql, conn))
        {
            cmd.Parameters.AddWithValue("shipment_id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            cmd.Parameters.AddWithValue("rating", request.Rating);
            cmd.Parameters.AddWithValue("comment", (object?)comment ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            feedbackId = reader.GetGuid(0);
            createdAt = reader.GetDateTime(1);
        }

        _logger.LogInformation("Customer {CustomerId} created feedback {FeedbackId} for shipment {ShipmentId}, rating={Rating}",
            customerId, feedbackId, shipmentId, request.Rating);

        return StatusCode(201, new
        {
            id = feedbackId,
            shipmentId,
            rating = request.Rating,
            comment,
            createdAt,
            message = "Đánh giá đã được gửi thành công."
        });
    }

    // ─── POST /api/customer/feedbacks/{feedbackId}/evidence ────────
    /// <summary>
    /// Upload evidence images for a feedback. multipart/form-data with files[].
    /// Max 5 images total per feedback, each max 10MB.
    /// Allowed: image/jpeg, image/png, image/webp.
    /// </summary>
    [HttpPost("feedbacks/{feedbackId:guid}/evidence")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)] // 50MB total
    public async Task<IActionResult> UploadEvidence(
        Guid feedbackId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        var customerId = GetCurrentUserId();

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // 1. Verify feedback exists and belongs to this customer
        const string checkSql = """
            SELECT id FROM warehouse.shipment_feedbacks
            WHERE id = @feedback_id AND customer_id = @customer_id;
        """;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("feedback_id", feedbackId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy đánh giá hoặc bạn không có quyền truy cập." });
        }

        // 2. Validate files
        if (files == null || files.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất một file." });

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        const int maxFiles = 5;

        if (files.Count > maxFiles)
            return BadRequest(new { message = $"Tối đa {maxFiles} ảnh cho mỗi đánh giá." });

        // Check current evidence count + new files
        const string countSql = """
            SELECT COUNT(*) FROM warehouse.shipment_feedback_evidence WHERE feedback_id = @feedback_id;
        """;
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            countCmd.Parameters.AddWithValue("feedback_id", feedbackId);
            var currentCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(ct) ?? 0);
            if (currentCount + files.Count > maxFiles)
                return BadRequest(new { message = $"Tối đa {maxFiles} ảnh cho mỗi đánh giá. Hiện có {currentCount} ảnh." });
        }

        var uploadedDtos = new List<object>();

        const string insertEvidenceSql = """
            INSERT INTO warehouse.shipment_feedback_evidence
                (feedback_id, storage_key, original_file_name, content_type, file_size, uploaded_by, uploaded_at)
            VALUES
                (@feedback_id, @storage_key, @original_file_name, @content_type, @file_size, @uploaded_by, @uploaded_at)
            RETURNING id;
        """;

        foreach (var file in files)
        {
            // Validate type
            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new { message = $"Định dạng file không hợp lệ: {file.ContentType}. Chỉ chấp nhận JPG, PNG, WEBP." });

            // Validate size
            if (file.Length > maxFileSize)
                return BadRequest(new { message = $"File {file.FileName} vượt quá giới hạn 10MB." });

            // Save file
            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(file.OpenReadStream(), file.FileName, file.ContentType, ct);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            // Insert evidence metadata
            await using var evCmd = new NpgsqlCommand(insertEvidenceSql, conn);
            evCmd.Parameters.AddWithValue("feedback_id", feedbackId);
            evCmd.Parameters.AddWithValue("storage_key", storageKey);
            evCmd.Parameters.AddWithValue("original_file_name", (object?)file.FileName ?? DBNull.Value);
            evCmd.Parameters.AddWithValue("content_type", (object?)file.ContentType ?? DBNull.Value);
            evCmd.Parameters.AddWithValue("file_size", file.Length);
            evCmd.Parameters.AddWithValue("uploaded_by", customerId);
            evCmd.Parameters.AddWithValue("uploaded_at", DateTime.UtcNow);
            var evidenceId = await evCmd.ExecuteScalarAsync(ct);

            uploadedDtos.Add(new { id = evidenceId, fileName = file.FileName, storageKey });
        }

        // Update updated_at on feedback
        const string updateTimestamp = """
            UPDATE warehouse.shipment_feedbacks SET updated_at = NOW() WHERE id = @feedback_id;
        """;
        await using (var tsCmd = new NpgsqlCommand(updateTimestamp, conn))
        {
            tsCmd.Parameters.AddWithValue("feedback_id", feedbackId);
            await tsCmd.ExecuteNonQueryAsync(ct);
        }

        return Ok(new { evidence = uploadedDtos, message = "Tải ảnh thành công." });
    }

    // ─── GET /api/customer/shipments/{shipmentId}/feedback ──────────
    /// <summary>
    /// Get the customer's own feedback for a specific shipment.
    /// </summary>
    [HttpGet("shipments/{shipmentId:guid}/feedback")]
    public async Task<IActionResult> GetMyFeedback(Guid shipmentId, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT sf.id, sf.shipment_id, sf.customer_id, sf.rating, sf.comment,
                   sf.created_at, sf.updated_at,
                   s.shipment_code
            FROM warehouse.shipment_feedbacks sf
            JOIN warehouse.shipments s ON s.id = sf.shipment_id
            WHERE sf.shipment_id = @shipment_id AND sf.customer_id = @customer_id;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        cmd.Parameters.AddWithValue("customer_id", customerId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Chưa có đánh giá cho đơn hàng này." });

        var feedbackId = reader.GetGuid(0);
        var rating = reader.GetInt32(3);
        var comment = reader.IsDBNull(4) ? null : reader.GetString(4);
        var createdAt = reader.GetDateTime(5);
        var updatedAt = reader.GetDateTime(6);
        var shipmentCode = reader.IsDBNull(7) ? null : reader.GetString(7);

        await reader.CloseAsync();

        // Load evidence
        var evidence = await LoadEvidenceAsync(conn, feedbackId, ct);

        return Ok(new
        {
            id = feedbackId,
            shipmentId,
            shipmentCode,
            rating,
            comment,
            createdAt,
            updatedAt,
            evidence
        });
    }

    // ─── GET /api/customer/feedbacks/{feedbackId}/evidence/{evidenceId} ──
    /// <summary>
    /// Download an evidence image for a customer's own feedback.
    /// </summary>
    [HttpGet("feedbacks/{feedbackId:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> DownloadEvidence(
        Guid feedbackId,
        Guid evidenceId,
        CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Verify feedback belongs to customer
        const string checkSql = """
            SELECT id FROM warehouse.shipment_feedbacks
            WHERE id = @feedback_id AND customer_id = @customer_id;
        """;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("feedback_id", feedbackId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            var checkResult = await cmd.ExecuteScalarAsync(ct);
            if (checkResult == null)
                return NotFound(new { message = "Không tìm thấy đánh giá hoặc bạn không có quyền truy cập." });
        }

        // Load evidence metadata
        const string evSql = """
            SELECT storage_key, original_file_name, content_type
            FROM warehouse.shipment_feedback_evidence
            WHERE id = @evidence_id AND feedback_id = @feedback_id;
        """;
        string storageKey = "", fileName = "", contentType = "";
        await using (var cmd = new NpgsqlCommand(evSql, conn))
        {
            cmd.Parameters.AddWithValue("evidence_id", evidenceId);
            cmd.Parameters.AddWithValue("feedback_id", feedbackId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return NotFound(new { message = "Không tìm thấy file evidences." });
            storageKey = reader.GetString(0);
            fileName = reader.IsDBNull(1) ? "evidence" : reader.GetString(1);
            contentType = reader.IsDBNull(2) ? "application/octet-stream" : reader.GetString(2);
        }

        // Stream file
        var result = await _fileStorage.OpenReadAsync(storageKey, ct);
        if (result == null)
            return NotFound(new { message = "File evidences không tồn tại trên hệ thống." });

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

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

// ─── Request DTOs ─────────────────────────────────────────────────────

public sealed record CreateFeedbackRequest
{
    public int Rating { get; init; }
    public string? Comment { get; init; }
}
