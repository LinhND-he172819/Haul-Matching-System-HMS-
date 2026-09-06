using System.Security.Claims;
using HMS.Modules.Warehouse.Application.DTOs.Customer;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Sms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/customer/shipments")]
[Authorize(Roles = "Customer")]
public class CustomerShipmentController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly ILogger<CustomerShipmentController> _logger;

    public CustomerShipmentController(
        IConfiguration configuration,
        ISmsNotificationService smsNotificationService,
        ILogger<CustomerShipmentController> logger)
    {
        _configuration = configuration;
        _smsNotificationService = smsNotificationService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        return userId;
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection") ?? "";

    // ─── GET /api/customer/shipments ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetMyShipments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Count
        const string countSql = """
            SELECT COUNT(*) FROM warehouse.shipments s
            WHERE s.customer_id = @customer_id AND s.is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(countSql, conn))
        {
            cmd.Parameters.AddWithValue("customer_id", customerId);
            var total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);

            var offset = Math.Max(0, (page - 1) * pageSize);

            const string listSql = """
                SELECT s.id, s.qr_code, s.cargo_type, s.weight_kg, s.volume_cbm,
                       s.receiver_name, s.dest_address, s.status,
                       COALESCE(t.trip_code, 'TRIP-' || LEFT(t.id::text, 8)) AS trip_code,
                       oh.name AS origin_name, dh.name AS destination_name,
                       s.created_at
                FROM warehouse.shipments s
                LEFT JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
                LEFT JOIN transport.trips t ON t.id = ts.trip_id AND t.is_deleted = FALSE
                LEFT JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
                LEFT JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
                WHERE s.customer_id = @customer_id AND s.is_deleted = FALSE
                ORDER BY
                    CASE s.status
                        WHEN 'PendingReview' THEN 1
                        WHEN 'PendingDeposit' THEN 2
                        WHEN 'Matched' THEN 3
                        WHEN 'In_Warehouse' THEN 4
                        WHEN 'In_Transit' THEN 5
                        WHEN 'Delivered' THEN 6
                        WHEN 'Draft' THEN 7
                        WHEN 'Completed' THEN 8
                        WHEN 'Cancelled' THEN 9
                        ELSE 10
                    END,
                    s.created_at DESC
                OFFSET @offset LIMIT @limit;
            """;

            await using var listCmd = new NpgsqlCommand(listSql, conn);
            listCmd.Parameters.AddWithValue("customer_id", customerId);
            listCmd.Parameters.AddWithValue("offset", offset);
            listCmd.Parameters.AddWithValue("limit", pageSize);

            var items = new List<CustomerShipmentListItem>();
            await using var reader = await listCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var status = reader.GetString(7);
                items.Add(new CustomerShipmentListItem
                {
                    Id = reader.GetGuid(0),
                    ShipmentCode = reader.GetString(1),
                    Commodity = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Weight = reader.GetDecimal(3),
                    Volume = reader.GetDecimal(4),
                    ReceiverName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DeliveryAddress = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Status = status,
                    TripCode = reader.IsDBNull(8) ? null : reader.GetString(8),
                    OriginName = reader.IsDBNull(9) ? null : reader.GetString(9),
                    DestinationName = reader.IsDBNull(10) ? null : reader.GetString(10),
                    CreatedAt = reader.GetDateTime(11),
                    AllowedActions = ComputeAllowedActions(status)
                });
            }

            return Ok(new PagedResult<CustomerShipmentListItem>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }
    }

    // ─── GET /api/customer/shipments/{shipmentId} ──────────────────────
    [HttpGet("{shipmentId:guid}")]
    public async Task<IActionResult> GetShipmentDetail(Guid shipmentId, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT s.id, s.qr_code, s.status, s.created_at, s.updated_at,
                   s.sender_name, s.sender_phone, s.pickup_address, s.pickup_note,
                   s.receiver_name, s.receiver_phone, s.dest_address,
                   s.cargo_type, s.weight_kg, s.volume_cbm, s.special_handling_note,
                   s.cancel_reason, s.cancelled_at, s.delivered_at, s.delivery_note,
                   COALESCE(t.trip_code, 'TRIP-' || LEFT(t.id::text, 8)) AS trip_code,
                   oh.name AS origin_name, dh.name AS destination_name,
                   t.started_at AS departure_time,
                   t.scheduled_departure_at AS scheduled_departure_at,
                   v.license_plate AS vehicle_plate
            FROM warehouse.shipments s
            LEFT JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
            LEFT JOIN transport.trips t ON t.id = ts.trip_id AND t.is_deleted = FALSE
            LEFT JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            LEFT JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            LEFT JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            WHERE s.id = @shipment_id AND s.customer_id = @customer_id AND s.is_deleted = FALSE
            LIMIT 1;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        cmd.Parameters.AddWithValue("customer_id", customerId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return NotFound(new { message = "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập." });

        var status = reader.GetString(2);
        var detail = new CustomerShipmentDetail
        {
            Id = reader.GetGuid(0),
            ShipmentCode = reader.GetString(1),
            Status = status,
            CreatedAt = reader.GetDateTime(3),
            UpdatedAt = reader.GetDateTime(4),
            SenderName = reader.IsDBNull(5) ? null : reader.GetString(5),
            SenderPhone = reader.IsDBNull(6) ? null : reader.GetString(6),
            PickupAddress = reader.IsDBNull(7) ? null : reader.GetString(7),
            PickupNote = reader.IsDBNull(8) ? null : reader.GetString(8),
            ReceiverName = reader.IsDBNull(9) ? null : reader.GetString(9),
            ReceiverPhone = reader.IsDBNull(10) ? null : reader.GetString(10),
            DeliveryAddress = reader.IsDBNull(11) ? null : reader.GetString(11),
            Commodity = reader.IsDBNull(12) ? null : reader.GetString(12),
            Weight = reader.GetDecimal(13),
            Volume = reader.GetDecimal(14),
            SpecialInstructions = reader.IsDBNull(15) ? null : reader.GetString(15),
            TripCode = reader.IsDBNull(20) ? null : reader.GetString(20),
            OriginName = reader.IsDBNull(21) ? null : reader.GetString(21),
            DestinationName = reader.IsDBNull(22) ? null : reader.GetString(22),
            DepartureTime = reader.IsDBNull(23) ? (DateTimeOffset?)null : reader.GetDateTime(23),
            ScheduledDepartureAt = reader.IsDBNull(25) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(25),
            VehiclePlate = reader.IsDBNull(24) ? null : reader.GetString(24),
            AllowedActions = ComputeAllowedActions(status)
        };

        await reader.CloseAsync();

        // Load proposal info
        detail = detail with { Proposal = await LoadProposalInfo(conn, shipmentId, ct) };

        // Load quotation info
        detail = detail with { Quotation = await LoadQuotationInfo(conn, shipmentId, ct) };

        // Load payment summary
        detail = detail with { Payment = await LoadPaymentSummary(conn, shipmentId, ct) };

        // Load timeline
        detail = detail with { Timeline = await LoadTimeline(conn, shipmentId, ct) };

        return Ok(detail);
    }

    // ─── PUT /api/customer/shipments/{shipmentId} ──────────────────────
    [HttpPut("{shipmentId:guid}")]
    public async Task<IActionResult> UpdateDraft(Guid shipmentId, [FromBody] UpdateDraftShipmentRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Verify ownership and status
        const string checkSql = """
            SELECT id, status FROM warehouse.shipments
            WHERE id = @id AND customer_id = @customer_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
            var status = reader.GetString(1);
            if (status != "Draft")
                return Conflict(new { message = "Chỉ có thể cập nhật đơn hàng đang ở trạng thái bản nháp." });
        }

        // Update
        const string updateSql = """
            UPDATE warehouse.shipments SET
                receiver_name = COALESCE(@receiver_name, receiver_name),
                receiver_phone = COALESCE(@receiver_phone, receiver_phone),
                dest_address = COALESCE(@dest_address, dest_address),
                cargo_type = COALESCE(@cargo_type, cargo_type),
                weight_kg = COALESCE(@weight_kg, weight_kg),
                volume_cbm = COALESCE(@volume_cbm, volume_cbm),
                special_handling_note = COALESCE(@special_handling_note, special_handling_note),
                sender_name = COALESCE(@sender_name, sender_name),
                sender_phone = COALESCE(@sender_phone_sender, sender_phone),
                pickup_address = COALESCE(@pickup_address, pickup_address),
                pickup_note = COALESCE(@pickup_note, pickup_note),
                updated_at = NOW()
            WHERE id = @id AND customer_id = @customer_id AND is_deleted = FALSE
            RETURNING id;
        """;

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            cmd.Parameters.AddWithValue("receiver_name", (object?)request.ReceiverName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("receiver_phone", (object?)request.ReceiverPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("dest_address", (object?)request.DeliveryAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cargo_type", (object?)request.Commodity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("weight_kg", (object?)request.Weight ?? DBNull.Value);
            cmd.Parameters.AddWithValue("volume_cbm", (object?)request.Volume ?? DBNull.Value);
            cmd.Parameters.AddWithValue("special_handling_note", (object?)request.SpecialInstructions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sender_name", (object?)request.SenderName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sender_phone_sender", (object?)request.SenderPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pickup_address", (object?)request.PickupAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pickup_note", (object?)request.PickupNote ?? DBNull.Value);

            var updated = await cmd.ExecuteScalarAsync(ct);
            if (updated == null)
                return Conflict(new { message = "Không thể cập nhật đơn hàng." });
        }

        // Audit
        await InsertAuditLog(conn, "Shipment", shipmentId, "Updated", customerId, ct);

        return Ok(new { message = "Cập nhật thành công." });
    }

    // ─── POST /api/customer/shipments/{shipmentId}/cancel ──────────────
    [HttpPost("{shipmentId:guid}/cancel")]
    public async Task<IActionResult> CancelShipment(Guid shipmentId, [FromBody] CancelShipmentRequest request, CancellationToken ct)
    {
        var customerId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Lý do hủy là bắt buộc." });

        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);

        // Read current status
        string currentStatus;
        const string checkSql = """
            SELECT status FROM warehouse.shipments
            WHERE id = @id AND customer_id = @customer_id AND is_deleted = FALSE;
        """;
        await using (var cmd = new NpgsqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("id", shipmentId);
            cmd.Parameters.AddWithValue("customer_id", customerId);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
            currentStatus = result.ToString()!;
        }

        // Validate cancellation allowed
        var cancellableStatuses = new[] { "Draft", "PendingReview", "PendingDeposit" };
        if (!cancellableStatuses.Contains(currentStatus))
            return Conflict(new { message = $"Chỉ có thể hủy đơn hàng ở trạng thái: Bản nháp, Chờ duyệt, Chờ đặt cọc. Trạng thái hiện tại: {currentStatus}." });

        // For PendingDeposit: check no paid payment
        if (currentStatus == "PendingDeposit")
        {
            const string paymentCheck = """
                SELECT COUNT(*) FROM warehouse.payments
                WHERE shipment_id = @shipment_id AND status = 'Paid' AND is_deleted = FALSE;
            """;
            await using var payCmd = new NpgsqlCommand(paymentCheck, conn);
            payCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            var paidCount = Convert.ToInt32(await payCmd.ExecuteScalarAsync(ct) ?? 0);
            if (paidCount > 0)
                return Conflict(new { message = "Đơn hàng đã phát sinh thanh toán. Vui lòng liên hệ bộ phận điều phối để yêu cầu hủy." });
        }

        // Transaction: cancel shipment + related entities
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Cancel shipment
            const string cancelSql = """
                UPDATE warehouse.shipments SET
                    status = 'Cancelled',
                    cancel_reason = @reason,
                    cancelled_at = NOW(),
                    cancelled_by = @customer_id,
                    updated_at = NOW()
                WHERE id = @id AND customer_id = @customer_id AND is_deleted = FALSE;
            """;
            await using (var cmd = new NpgsqlCommand(cancelSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("id", shipmentId);
                cmd.Parameters.AddWithValue("customer_id", customerId);
                cmd.Parameters.AddWithValue("reason", request.Reason);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Cancel related proposal if PendingReview
            if (currentStatus == "PendingReview")
            {
                const string cancelProposal = """
                    UPDATE warehouse.shipment_proposals SET
                        status = 'Cancelled',
                        cancelled_at = NOW()
                    WHERE shipment_id = @shipment_id AND status = 'Pending';
                """;
                await using var cmd = new NpgsqlCommand(cancelProposal, conn, tx);
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Cancel related proposal + quotation + payment if PendingDeposit
            if (currentStatus == "PendingDeposit")
            {
                const string cancelProposal = """
                    UPDATE warehouse.shipment_proposals SET
                        status = 'Cancelled',
                        cancelled_at = NOW()
                    WHERE shipment_id = @shipment_id AND status IN ('Pending', 'Accepted');
                """;
                await using var cmd = new NpgsqlCommand(cancelProposal, conn, tx);
                cmd.Parameters.AddWithValue("shipment_id", shipmentId);
                await cmd.ExecuteNonQueryAsync(ct);

                const string cancelQuotation = """
                    UPDATE warehouse.quotations SET status = 'Cancelled', updated_at = NOW()
                    WHERE shipment_id = @shipment_id AND status IN ('Draft', 'Sent');
                """;
                await using var qCmd = new NpgsqlCommand(cancelQuotation, conn, tx);
                qCmd.Parameters.AddWithValue("shipment_id", shipmentId);
                await qCmd.ExecuteNonQueryAsync(ct);

                const string cancelPayment = """
                    UPDATE warehouse.payments SET status = 'Cancelled', updated_at = NOW()
                    WHERE shipment_id = @shipment_id AND status = 'Pending';
                """;
                await using var pCmd = new NpgsqlCommand(cancelPayment, conn, tx);
                pCmd.Parameters.AddWithValue("shipment_id", shipmentId);
                await pCmd.ExecuteNonQueryAsync(ct);
            }

            // Audit log
            const string auditSql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES ('Shipment', @entity_id, 'Cancelled', @performed_by, @details::jsonb, NOW());
            """;
            await using var auditCmd = new NpgsqlCommand(auditSql, conn, tx);
            auditCmd.Parameters.AddWithValue("entity_id", shipmentId);
            auditCmd.Parameters.AddWithValue("performed_by", customerId);
            auditCmd.Parameters.AddWithValue("details", System.Text.Json.JsonSerializer.Serialize(new { reason = request.Reason, fromStatus = currentStatus }));
            await auditCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // SMS notification (after commit, non-blocking)
        try
        {
            string shipmentCode = "";
            const string readCodeSql = "SELECT shipment_code FROM warehouse.shipments WHERE id = @id;";
            await using (var scCmd = new NpgsqlCommand(readCodeSql, conn))
            {
                scCmd.Parameters.AddWithValue("id", shipmentId);
                shipmentCode = (await scCmd.ExecuteScalarAsync(ct)) as string ?? "";
            }
            if (!string.IsNullOrEmpty(shipmentCode))
            {
                await _smsNotificationService.SendShipmentStatusAsync(
                    shipmentId, SmsMessageType.ShipmentCancelled, shipmentCode, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send ShipmentCancelled SMS for shipment {ShipmentId}", shipmentId);
        }

        return Ok(new { message = "Đã hủy đơn hàng thành công." });
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static AllowedActions ComputeAllowedActions(string status) => status switch
    {
        "Draft" => new() { CanView = true, CanEdit = true, CanCancel = true, CanPayDeposit = false, CanPayRemaining = false },
        "PendingReview" => new() { CanView = true, CanEdit = false, CanCancel = true, CanPayDeposit = false, CanPayRemaining = false },
        "PendingDeposit" => new() { CanView = true, CanEdit = false, CanCancel = true, CanPayDeposit = true, CanPayRemaining = false },
        "Matched" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = false },
        "In_Warehouse" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = false },
        "In_Transit" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = false },
        "Delivered" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = true },
        "Completed" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = false, CanGiveFeedback = true },
        "Cancelled" => new() { CanView = true, CanEdit = false, CanCancel = false, CanPayDeposit = false, CanPayRemaining = false },
        _ => new() { CanView = true }
    };

    private static async Task<ProposalInfo?> LoadProposalInfo(NpgsqlConnection conn, Guid shipmentId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, status, created_at, reviewed_at, reject_reason
            FROM warehouse.shipment_proposals
            WHERE shipment_id = @shipment_id
            ORDER BY created_at DESC LIMIT 1;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ProposalInfo
        {
            Id = reader.GetGuid(0),
            Status = reader.GetString(1),
            SubmittedAt = reader.GetDateTime(2),
            ReviewedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            RejectReason = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    private static async Task<QuotationInfo?> LoadQuotationInfo(NpgsqlConnection conn, Guid shipmentId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, quotation_code, shipping_fee, deposit_amount,
                   (shipping_fee - deposit_amount) AS remaining_amount,
                   sent_at, expires_at, status
            FROM warehouse.quotations
            WHERE shipment_id = @shipment_id AND is_deleted = FALSE
            ORDER BY created_at DESC LIMIT 1;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new QuotationInfo
        {
            Id = reader.GetGuid(0),
            QuotationCode = reader.GetString(1),
            ShippingFee = reader.GetDecimal(2),
            DepositAmount = reader.GetDecimal(3),
            RemainingAmount = reader.GetDecimal(4),
            SentAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            ExpiresAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            Status = reader.GetString(7)
        };
    }

    private static async Task<PaymentSummary> LoadPaymentSummary(NpgsqlConnection conn, Guid shipmentId, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(SUM(CASE WHEN payment_type = 'Deposit' AND status = 'Paid' THEN amount ELSE 0 END), 0) AS deposit_paid,
                   COALESCE(SUM(CASE WHEN payment_type = 'FinalPayment' AND status = 'Paid' THEN amount ELSE 0 END), 0) AS final_paid
            FROM warehouse.payments
            WHERE shipment_id = @shipment_id AND is_deleted = FALSE;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new PaymentSummary();

        var depositPaid = reader.GetDecimal(0);
        var finalPaid = reader.GetDecimal(1);
        var totalPaid = depositPaid + finalPaid;

        // Get outstanding from quotation (shipping_fee - deposit_amount)
        decimal outstanding = 0;
        try
        {
            const string quoteSql = """
                SELECT (shipping_fee - deposit_amount) AS remaining
                FROM warehouse.quotations
                WHERE shipment_id = @shipment_id AND is_deleted = FALSE
                ORDER BY created_at DESC LIMIT 1;
            """;
            await using var quoteCmd = new NpgsqlCommand(quoteSql, conn);
            quoteCmd.Parameters.AddWithValue("shipment_id", shipmentId);
            await using var quoteReader = await quoteCmd.ExecuteReaderAsync(ct);
            if (await quoteReader.ReadAsync(ct))
            {
                var remaining = quoteReader.GetDecimal(0);
                outstanding = remaining - finalPaid;
                if (outstanding < 0) outstanding = 0;
            }
        }
        catch { /* If quotation not found, outstanding stays 0 */ }

        string? paymentStatus;
        if (totalPaid <= 0) paymentStatus = "Unpaid";
        else if (outstanding > 0) paymentStatus = "Partial";
        else paymentStatus = "Completed";

        return new PaymentSummary
        {
            DepositPaid = depositPaid,
            FinalPaid = finalPaid,
            TotalPaid = totalPaid,
            OutstandingAmount = outstanding,
            PaymentStatus = paymentStatus
        };
    }

    private static async Task<List<TimelineEntry>> LoadTimeline(NpgsqlConnection conn, Guid shipmentId, CancellationToken ct)
    {
        const string sql = """
            SELECT to_status, occurred_at
            FROM warehouse.shipment_status_history
            WHERE shipment_id = @shipment_id
            ORDER BY occurred_at ASC;
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("shipment_id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var timeline = new List<TimelineEntry>();
        while (await reader.ReadAsync(ct))
        {
            var toStatus = reader.GetString(0);
            var occurredAt = reader.GetDateTime(1);
            timeline.Add(new TimelineEntry
            {
                Label = StatusToVietnamese(toStatus),
                Timestamp = occurredAt,
                IsCompleted = true,
                IsCurrent = false
            });
        }
        return timeline;
    }

    private static async Task InsertAuditLog(NpgsqlConnection conn, string entityType, Guid entityId, string action, Guid performedBy, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, created_at)
            VALUES (@entity_type, @entity_id, @action, @performed_by, NOW());
        """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("entity_type", entityType);
        cmd.Parameters.AddWithValue("entity_id", entityId);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("performed_by", performedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string StatusToVietnamese(string status) => status switch
    {
        "Draft" => "Bản nháp",
        "PendingReview" => "Chờ duyệt",
        "PendingDeposit" => "Chờ đặt cọc",
        "In_Warehouse" => "Đã vào kho",
        "Matched" => "Đã ghép chuyến",
        "In_Transit" => "Đang vận chuyển",
        "Delivered" => "Đã giao hàng",
        "Completed" => "Hoàn tất",
        "Cancelled" => "Đã hủy",
        _ => status
    };
}
