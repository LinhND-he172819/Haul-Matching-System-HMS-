using HMS.Modules.Warehouse.Application.DTOs;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Warehouse.Application.Services;

public class HubInventoryService : IHubInventoryService
{
    private readonly string _connStr;

    public HubInventoryService(IConfiguration configuration)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=hms_db;Username=postgres;Password=lytruongtho123";
    }

    public async Task<PagedResult<HubInventoryShipmentDto>> GetInventoryAsync(
        HubInventoryQuery q, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Build WHERE clauses dynamically
        var conditions = new List<string> { "s.is_deleted = FALSE" };
        var parameters = new List<NpgsqlParameter>();
        var paramIdx = 0;

        if (q.HubId.HasValue)
        {
            conditions.Add($"s.current_hub_id = @p{paramIdx}");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", q.HubId.Value));
            paramIdx++;
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            conditions.Add($"s.status = @p{paramIdx}");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", q.Status));
            paramIdx++;
        }
        else
        {
            // Mặc định chỉ hiển thị In_Warehouse & Returned
            conditions.Add($"s.status IN ('In_Warehouse', 'Returned')");
        }

        if (!string.IsNullOrWhiteSpace(q.CargoType))
        {
            conditions.Add($"s.cargo_type = @p{paramIdx}");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", q.CargoType));
            paramIdx++;
        }

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            conditions.Add($"(s.qr_code ILIKE @p{paramIdx} OR s.receiver_name ILIKE @p{paramIdx} OR s.receiver_phone ILIKE @p{paramIdx})");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", $"%{q.Keyword}%"));
            paramIdx++;
        }

        if (!string.IsNullOrWhiteSpace(q.FromDate) && DateTime.TryParse(q.FromDate, out var fromDate))
        {
            conditions.Add($"s.intake_confirmed_at >= @p{paramIdx}");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", fromDate));
            paramIdx++;
        }

        if (!string.IsNullOrWhiteSpace(q.ToDate) && DateTime.TryParse(q.ToDate, out var toDate))
        {
            conditions.Add($"s.intake_confirmed_at <= @p{paramIdx}");
            parameters.Add(new NpgsqlParameter($"p{paramIdx}", toDate.AddDays(1)));
            paramIdx++;
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        // Sort
        var sortClause = q.Sort?.ToLower() switch
        {
            "oldest" => "s.created_at ASC",
            "weight_asc" => "s.weight_kg ASC",
            "weight_desc" => "s.weight_kg DESC",
            "longest_storage" => "COALESCE(s.intake_confirmed_at, s.created_at) ASC",
            _ => "s.created_at DESC" // newest (default)
        };

        // Count total
        var countSql = $@"
            SELECT COUNT(*)
            FROM warehouse.shipments s
            LEFT JOIN identity.hubs h ON h.id = s.current_hub_id
            {whereClause}";

        await using var countCmd = new NpgsqlCommand(countSql, conn);
        foreach (var p in parameters) countCmd.Parameters.Add((NpgsqlParameter)p.Clone());
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Fetch page
        var offset = (q.Page - 1) * q.PageSize;
        var dataSql = $@"
            SELECT
                s.id,
                s.qr_code,
                s.cargo_type,
                s.receiver_name,
                s.receiver_phone,
                s.dest_address,
                s.status,
                s.current_hub_id,
                h.name AS hub_name,
                s.weight_kg,
                s.volume_cbm,
                s.cod_amount,
                s.shipping_fee,
                s.special_handling_note,
                s.created_at,
                s.intake_confirmed_at
            FROM warehouse.shipments s
            LEFT JOIN identity.hubs h ON h.id = s.current_hub_id
            {whereClause}
            ORDER BY {sortClause}
            LIMIT @limit OFFSET @offset";

        await using var dataCmd = new NpgsqlCommand(dataSql, conn);
        foreach (var p in parameters) dataCmd.Parameters.Add((NpgsqlParameter)p.Clone());
        dataCmd.Parameters.AddWithValue("limit", q.PageSize);
        dataCmd.Parameters.AddWithValue("offset", offset);

        var items = new List<HubInventoryShipmentDto>();
        await using var reader = await dataCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var intakeDate = reader.IsDBNull(15) ? (DateTime?)null : reader.GetDateTime(15);
            var createdDate = reader.GetDateTime(14);
            var referenceDate = intakeDate ?? createdDate;
            var daysInWarehouse = (int)(DateTime.UtcNow - referenceDate).TotalDays;

            items.Add(new HubInventoryShipmentDto
            {
                Id = reader.GetGuid(0),
                QrCode = reader.GetString(1),
                CargoType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ReceiverName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ReceiverPhone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                DestAddress = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Status = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CurrentHubId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                CurrentHubName = reader.IsDBNull(8) ? null : reader.GetString(8),
                WeightKg = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                VolumeCbm = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                Cod = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                ShippingFee = reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                SpecialHandlingNote = reader.IsDBNull(13) ? null : reader.GetString(13),
                CreatedAt = createdDate,
                IntakeConfirmedAt = intakeDate,
                DaysInWarehouse = daysInWarehouse
            });
        }

        return new PagedResult<HubInventoryShipmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    public async Task<HubInventoryDetailDto?> GetDetailAsync(Guid shipmentId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                s.id,
                s.qr_code,
                s.status,
                s.current_hub_id,
                h.name AS hub_name,
                s.created_at,
                s.intake_confirmed_at,
                c.full_name AS customer_name,
                c.phone AS customer_phone,
                s.receiver_name,
                s.receiver_phone,
                s.dest_address,
                s.cargo_type,
                s.weight_kg,
                s.volume_cbm,
                s.cod_amount,
                s.shipping_fee,
                s.special_handling_note,
                s.intake_confirmed_by,
                intake_staff.full_name AS intake_staff_name
            FROM warehouse.shipments s
            LEFT JOIN identity.hubs h ON h.id = s.current_hub_id
            LEFT JOIN identity.users c ON c.id = s.customer_id
            LEFT JOIN identity.users intake_staff ON intake_staff.id = s.intake_confirmed_by
            WHERE s.id = @id AND s.is_deleted = FALSE";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", shipmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        var createdAt = reader.GetDateTime(5);
        var intakeDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
        var referenceDate = intakeDate ?? createdAt;
        var daysInWarehouse = (int)(DateTime.UtcNow - referenceDate).TotalDays;
        var status = reader.IsDBNull(2) ? "" : reader.GetString(2);

        return new HubInventoryDetailDto
        {
            Id = reader.GetGuid(0),
            QrCode = reader.GetString(1),
            Status = status,
            CurrentHubId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
            CurrentHubName = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = createdAt,
            IntakeConfirmedAt = intakeDate,
            CustomerName = reader.IsDBNull(7) ? null : reader.GetString(7),
            CustomerPhone = reader.IsDBNull(8) ? null : reader.GetString(8),
            ReceiverName = reader.IsDBNull(9) ? "" : reader.GetString(9),
            ReceiverPhone = reader.IsDBNull(10) ? "" : reader.GetString(10),
            DestAddress = reader.IsDBNull(11) ? "" : reader.GetString(11),
            CargoType = reader.IsDBNull(12) ? "" : reader.GetString(12),
            WeightKg = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
            VolumeCbm = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
            Cod = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
            ShippingFee = reader.IsDBNull(16) ? null : reader.GetDecimal(16),
            SpecialHandlingNote = reader.IsDBNull(17) ? null : reader.GetString(17),
            IntakeStaffName = reader.IsDBNull(19) ? null : reader.GetString(19),
            DaysInWarehouse = daysInWarehouse,
            Timeline = BuildTimeline(reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6), status, createdAt)
        };
    }

    public async Task UpdateShipmentAsync(
        Guid shipmentId, UpdateShipmentRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        var setClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var idx = 0;

        if (request.ReceiverName != null)
        {
            setClauses.Add($"receiver_name = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.ReceiverName));
        }
        if (request.ReceiverPhone != null)
        {
            setClauses.Add($"receiver_phone = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.ReceiverPhone));
        }
        if (request.DestAddress != null)
        {
            setClauses.Add($"dest_address = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.DestAddress));
        }
        if (request.CargoType != null)
        {
            setClauses.Add($"cargo_type = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.CargoType));
        }
        if (request.WeightKg.HasValue)
        {
            setClauses.Add($"weight_kg = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.WeightKg.Value));
        }
        if (request.VolumeCbm.HasValue)
        {
            setClauses.Add($"volume_cbm = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.VolumeCbm.Value));
        }
        if (request.SpecialHandlingNote != null)
        {
            setClauses.Add($"special_handling_note = @p{idx}");
            parameters.Add(new NpgsqlParameter($"p{idx++}", request.SpecialHandlingNote));
        }

        if (setClauses.Count == 0)
            return;

        setClauses.Add("updated_at = now()");

        var sql = $@"UPDATE warehouse.shipments
            SET {string.Join(", ", setClauses)}
            WHERE id = @shipmentId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);
        cmd.Parameters.AddWithValue("shipmentId", shipmentId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<HubInventoryDashboardDto> GetDashboardSummaryAsync(
        Guid? hubId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        var hubFilter = hubId.HasValue ? "AND s.current_hub_id = @hubId" : "";
        var sql = $@"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE s.status = 'In_Warehouse') AS in_warehouse,
                COUNT(*) FILTER (WHERE s.status = 'Matched') AS matched,
                COUNT(*) FILTER (WHERE s.status = 'Assigned' OR s.status = 'Out_For_Delivery') AS ready,
                COUNT(*) FILTER (WHERE s.status = 'In_Warehouse' AND s.intake_confirmed_at < now() - INTERVAL '7 days') AS expired,
                COALESCE(SUM(s.weight_kg) FILTER (WHERE s.status IN ('In_Warehouse','Matched','Assigned')), 0) AS total_weight,
                COALESCE(SUM(s.volume_cbm) FILTER (WHERE s.status IN ('In_Warehouse','Matched','Assigned')), 0) AS total_volume
            FROM warehouse.shipments s
            WHERE s.is_deleted = FALSE AND s.status != 'Draft' AND s.status != 'Returned' {hubFilter}";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (hubId.HasValue)
            cmd.Parameters.AddWithValue("hubId", hubId.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new HubInventoryDashboardDto();

        return new HubInventoryDashboardDto
        {
            TotalShipment = reader.GetInt32(0),
            InWarehouse = reader.GetInt32(1),
            Matched = reader.GetInt32(2),
            ReadyForDispatch = reader.GetInt32(3),
            Expired = reader.GetInt32(4),
            TotalWeight = reader.GetDecimal(5),
            TotalVolume = reader.GetDecimal(6)
        };
    }

    private static List<TimelineEntry> BuildTimeline(
        DateTime? intakeConfirmedAt, string currentStatus, DateTime createdAt)
    {
        var timeline = new List<TimelineEntry>
        {
            new() { Label = "Draft", Timestamp = createdAt, IsCompleted = true, IsCurrent = currentStatus == "Draft" },
            new() { Label = "Intake Confirmed", Timestamp = intakeConfirmedAt, IsCompleted = intakeConfirmedAt.HasValue, IsCurrent = currentStatus == "In_Warehouse" },
            new() { Label = "Matching", Timestamp = null, IsCompleted = IsAfterStatus(currentStatus, "In_Warehouse"), IsCurrent = currentStatus == "Matched" },
            new() { Label = "Assigned", Timestamp = null, IsCompleted = IsAfterStatus(currentStatus, "Matched"), IsCurrent = currentStatus == "Assigned" },
            new() { Label = "Out For Delivery", Timestamp = null, IsCompleted = IsAfterStatus(currentStatus, "Assigned"), IsCurrent = currentStatus == "Out_For_Delivery" },
            new() { Label = "Delivered", Timestamp = null, IsCompleted = currentStatus == "Delivered", IsCurrent = currentStatus == "Delivered" },
        };
        return timeline;
    }

    private static bool IsAfterStatus(string current, string target)
    {
        var order = new[] { "Draft", "In_Warehouse", "Matched", "Assigned", "Out_For_Delivery", "Delivered", "Returned" };
        var currentIdx = Array.IndexOf(order, current);
        var targetIdx = Array.IndexOf(order, target);
        return currentIdx > targetIdx;
    }
}
