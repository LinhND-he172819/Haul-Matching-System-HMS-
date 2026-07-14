using HMS.Modules.Transport.Application.DTOs.TripPosts;
using HMS.Modules.Transport.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Transport.Infrastructure.Repositories;

public sealed class PostgresTripPostRepository : ITripPostRepository
{
    private readonly string _connectionString;

    public PostgresTripPostRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task<bool> HasOpenPostForTripAsync(Guid tripId, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(1)
            FROM transport.trip_posts
            WHERE trip_id = @tripId
              AND status = 'Open'
              AND is_deleted = FALSE;
            """;
        cmd.Parameters.AddWithValue("tripId", tripId);
        var count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        return count > 0;
    }

    public async Task<Guid> CreatePostAsync(TripPostRecord post, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO transport.trip_posts
                (id, trip_id, created_by, title, description, accept_until, status, published_at, closed_at, created_at, updated_at, is_deleted)
            VALUES
                (@id, @trip_id, @created_by, @title, @description, @accept_until, @status, @published_at, @closed_at, @created_at, @updated_at, FALSE);
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("trip_id", post.TripId);
        cmd.Parameters.AddWithValue("created_by", post.CreatedBy);
        cmd.Parameters.AddWithValue("title", post.Title);
        cmd.Parameters.Add("description", NpgsqlDbType.Text).Value = (object?)post.Description ?? DBNull.Value;
        cmd.Parameters.Add("accept_until", NpgsqlDbType.TimestampTz).Value = post.AcceptUntil.ToUniversalTime();
        cmd.Parameters.AddWithValue("status", post.Status);
        cmd.Parameters.Add("published_at", NpgsqlDbType.TimestampTz).Value = post.PublishedAt.HasValue
            ? post.PublishedAt.Value.ToUniversalTime() : (object)DBNull.Value;
        cmd.Parameters.Add("closed_at", NpgsqlDbType.TimestampTz).Value = post.ClosedAt.HasValue
            ? post.ClosedAt.Value.ToUniversalTime() : (object)DBNull.Value;
        cmd.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = post.CreatedAt.ToUniversalTime();
        cmd.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = post.UpdatedAt.ToUniversalTime();

        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<TripPostRecord?> GetPostByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, trip_id, created_by, title, description, accept_until, status, published_at, closed_at, created_at, updated_at
            FROM transport.trip_posts
            WHERE id = @id AND is_deleted = FALSE;
            """;
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new TripPostRecord
        {
            Id = reader.GetGuid(0),
            TripId = reader.GetGuid(1),
            CreatedBy = reader.GetGuid(2),
            Title = reader.GetString(3),
            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
            AcceptUntil = reader.GetDateTime(5),
            Status = reader.GetString(6),
            PublishedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            ClosedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            CreatedAt = reader.GetDateTime(9),
            UpdatedAt = reader.GetDateTime(10),
        };
    }

    public async Task UpdatePostAsync(TripPostRecord post, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE transport.trip_posts
            SET description = @description,
                accept_until = @accept_until,
                title = @title,
                status = @status,
                closed_at = @closed_at,
                updated_at = @updated_at
            WHERE id = @id AND is_deleted = FALSE;
            """;
        cmd.Parameters.AddWithValue("id", post.Id);
        cmd.Parameters.Add("description", NpgsqlDbType.Text).Value = (object?)post.Description ?? DBNull.Value;
        cmd.Parameters.Add("accept_until", NpgsqlDbType.TimestampTz).Value = post.AcceptUntil.ToUniversalTime();
        cmd.Parameters.AddWithValue("title", post.Title);
        cmd.Parameters.AddWithValue("status", post.Status);
        cmd.Parameters.Add("closed_at", NpgsqlDbType.TimestampTz).Value = post.ClosedAt.HasValue
            ? post.ClosedAt.Value.ToUniversalTime() : (object)DBNull.Value;
        cmd.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = post.UpdatedAt.ToUniversalTime();

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> CountPostsAsync(
        string? status, Guid? hubId, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var (where, ps) = BuildListWhereClause(status, hubId, fromDate, toDate);
        cmd.Parameters.AddRange(ps);
        cmd.CommandText = $"SELECT COUNT(1) FROM transport.trip_posts tp {where}";

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
    }

    public async Task<IReadOnlyList<TripPostListItemResponse>> ListPostsAsync(
        TripPostFilterRequest filter, Guid? resolvedHubId, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        // BuildListWhereClause already returns 'WHERE tp.is_deleted = FALSE ...'
        var (where, ps) = BuildListWhereClause(filter.Status, resolvedHubId, filter.FromDate, filter.ToDate);
        cmd.Parameters.AddRange(ps);

        // Keyword search
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = $"%{filter.Keyword}%";
            where += """
                AND (tp.title ILIKE @kw
                     OR u.full_name ILIKE @kw
                     OR v.license_plate ILIKE @kw
                     OR oh.name ILIKE @kw
                     OR dh.name ILIKE @kw)
                """;
            cmd.Parameters.AddWithValue("kw", NpgsqlDbType.Text, kw);
        }

        // Sorting
        var sortCol = filter.SortBy?.ToLowerInvariant() switch
        {
            "title" => "tp.title",
            "status" => "tp.status",
            "acceptuntil" or "accept_until" => "tp.accept_until",
            "publishedat" or "published_at" => "tp.published_at",
            "createdat" or "created_at" or _ => "tp.created_at"
        };
        var sortDir = string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        var offset = Math.Max(0, (filter.Page - 1) * filter.PageSize);
        var limit = filter.PageSize;

        // 'where' already contains 'WHERE tp.is_deleted = FALSE ...' from BuildListWhereClause
        cmd.CommandText = $"""
            SELECT
                tp.id, tp.trip_id, tp.title, tp.description,
                oh.name, dh.name,
                u.full_name,
                v.license_plate,
                (v.max_weight_kg - t.current_load_weight) AS remaining_weight,
                (v.max_volume_cbm - t.current_load_volume) AS remaining_volume,
                tp.status, tp.accept_until, tp.published_at,
                cu.full_name AS created_by_name
            FROM transport.trip_posts tp
            JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            JOIN identity.users u ON u.id = t.driver_id AND u.is_deleted = FALSE
            JOIN identity.users cu ON cu.id = tp.created_by
            {where}
            ORDER BY {sortCol} {sortDir}
            OFFSET @offset LIMIT @limit;
            """;

        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        var items = new List<TripPostListItemResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new TripPostListItemResponse(
                Id: reader.GetGuid(0),
                TripId: reader.GetGuid(1),
                Title: reader.GetString(2),
                Description: reader.IsDBNull(3) ? null : reader.GetString(3),
                OriginHubName: reader.GetString(4),
                DestinationHubName: reader.GetString(5),
                DriverName: reader.GetString(6),
                LicensePlate: reader.GetString(7),
                RemainingWeightKg: reader.GetDecimal(8),
                RemainingVolumeCbm: reader.GetDecimal(9),
                Status: reader.GetString(10),
                AcceptUntil: reader.GetDateTime(11),
                PublishedAt: reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                CreatedByName: reader.GetString(13)
            ));
        }

        return items;
    }

    public async Task<IReadOnlyList<PublicTripPostResponse>> ListPublicPostsAsync(
        PublicTripPostFilterRequest filter, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var conditions = new List<string>
        {
            "tp.is_deleted = FALSE",
            "tp.status = 'Open'",
            "tp.accept_until > NOW()",
            "t.status = 'Active'",
            "t.is_deleted = FALSE"
        };

        if (filter.OriginHubId.HasValue)
        {
            conditions.Add("t.origin_hub_id = @originHubId");
            cmd.Parameters.AddWithValue("originHubId", filter.OriginHubId.Value);
        }
        if (filter.DestinationHubId.HasValue)
        {
            conditions.Add("t.dest_hub_id = @destHubId");
            cmd.Parameters.AddWithValue("destHubId", filter.DestinationHubId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = $"%{filter.Keyword}%";
            conditions.Add("(tp.title ILIKE @kw OR oh.name ILIKE @kw OR dh.name ILIKE @kw)");
            cmd.Parameters.AddWithValue("kw", NpgsqlDbType.Text, kw);
        }
        if (filter.DepartureFrom.HasValue)
        {
            conditions.Add("t.started_at >= @depFrom");
            cmd.Parameters.Add("depFrom", NpgsqlDbType.TimestampTz).Value = filter.DepartureFrom.Value.ToUniversalTime();
        }
        if (filter.DepartureTo.HasValue)
        {
            conditions.Add("t.started_at <= @depTo");
            cmd.Parameters.Add("depTo", NpgsqlDbType.TimestampTz).Value = filter.DepartureTo.Value.ToUniversalTime();
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var offset = Math.Max(0, (filter.Page - 1) * filter.PageSize);

        cmd.CommandText = $"""
            SELECT
                tp.id, tp.title, tp.description,
                oh.name, dh.name,
                t.started_at,
                tp.accept_until,
                (v.max_weight_kg - t.current_load_weight) AS remaining_weight,
                (v.max_volume_cbm - t.current_load_volume) AS remaining_volume,
                v.vehicle_type,
                v.license_plate
            FROM transport.trip_posts tp
            JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE AND t.status = 'Active'
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            {whereClause}
            ORDER BY tp.published_at DESC
            OFFSET @offset LIMIT @limit;
            """;

        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", filter.PageSize);

        var items = new List<PublicTripPostResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var remainingWeight = reader.GetDecimal(7);
            var remainingVolume = reader.GetDecimal(8);

            // Skip posts with zero remaining capacity
            if (remainingWeight <= 0 || remainingVolume <= 0) continue;

            items.Add(new PublicTripPostResponse(
                Id: reader.GetGuid(0),
                Title: reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                OriginHubName: reader.GetString(3),
                DestinationHubName: reader.GetString(4),
                DepartureTime: reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                AcceptUntil: reader.GetDateTime(6),
                RemainingWeightKg: remainingWeight,
                RemainingVolumeCbm: remainingVolume,
                TruckType: reader.GetString(9),
                LicensePlate: reader.GetString(10)
            ));
        }

        return items;
    }

    public async Task<TripPostKpiResponse> GetKpiAsync(Guid? hubId, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var hubFilter = hubId.HasValue ? "AND t.origin_hub_id = @hubId" : "";
        if (hubId.HasValue) cmd.Parameters.AddWithValue("hubId", hubId.Value);

        cmd.CommandText = $"""
            SELECT
                (SELECT COUNT(1) FROM transport.trip_posts tp
                 JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                 WHERE tp.is_deleted = FALSE {hubFilter}) AS total,
                (SELECT COUNT(1) FROM transport.trip_posts tp
                 JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                 WHERE tp.is_deleted = FALSE AND tp.status = 'Open' {hubFilter}) AS open_count,
                (SELECT COUNT(1) FROM transport.trip_posts tp
                 JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                 WHERE tp.is_deleted = FALSE AND tp.status = 'Closed' {hubFilter}) AS closed_count,
                (SELECT COUNT(1) FROM transport.trip_posts tp
                 JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                 WHERE tp.is_deleted = FALSE AND tp.status = 'Expired' {hubFilter}) AS expired_count,
                (SELECT COUNT(1) FROM transport.trip_posts tp
                 JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
                 WHERE tp.is_deleted = FALSE AND tp.status = 'Cancelled' {hubFilter}) AS cancelled_count;
            """;

        // Read the first query result and close the reader before running a second query
        int total = 0, openCount = 0, closedCount = 0, expiredCount = 0, cancelledCount = 0;
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                total = Convert.ToInt32(reader[0]);
                openCount = Convert.ToInt32(reader[1]);
                closedCount = Convert.ToInt32(reader[2]);
                expiredCount = Convert.ToInt32(reader[3]);
                cancelledCount = Convert.ToInt32(reader[4]);
            }
            else
            {
                return new TripPostKpiResponse(0, 0, 0, 0, 0, 0);
            }
        } // reader is disposed here, freeing the connection

        // Count eligible trips: Active trips with remaining capacity and no Open post
        await using var eligibleCmd = conn.CreateCommand();
        if (hubId.HasValue) eligibleCmd.Parameters.AddWithValue("hubId", hubId.Value);
        eligibleCmd.CommandText = $"""
            SELECT COUNT(1)
            FROM transport.trips t
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            WHERE t.status = 'Active' AND t.is_deleted = FALSE
              AND (v.max_weight_kg - t.current_load_weight) > 0
              AND (v.max_volume_cbm - t.current_load_volume) > 0
              AND NOT EXISTS (
                  SELECT 1 FROM transport.trip_posts tp2
                  WHERE tp2.trip_id = t.id AND tp2.status = 'Open' AND tp2.is_deleted = FALSE
              )
              {hubFilter};
            """;
        var eligibleTrips = Convert.ToInt32(await eligibleCmd.ExecuteScalarAsync(ct) ?? 0);

        return new TripPostKpiResponse(total, openCount, closedCount, expiredCount, cancelledCount, eligibleTrips);
    }

    public async Task<int> CountOpenPostsForHubAsync(Guid? hubId, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var hubFilter = hubId.HasValue ? "AND t.origin_hub_id = @hubId" : "";
        if (hubId.HasValue) cmd.Parameters.AddWithValue("hubId", hubId.Value);

        cmd.CommandText = $"""
            SELECT COUNT(1) FROM transport.trip_posts tp
            JOIN transport.trips t ON t.id = tp.trip_id AND t.is_deleted = FALSE
            WHERE tp.is_deleted = FALSE AND tp.status = 'Open' {hubFilter};
            """;

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
    }

    public async Task<int> ExpireOpenPostsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE transport.trip_posts
            SET status = 'Expired',
                closed_at = NOW(),
                updated_at = NOW()
            WHERE status = 'Open'
              AND accept_until <= NOW()
              AND is_deleted = FALSE;
            """;

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static (string whereClause, NpgsqlParameter[] parameters) BuildListWhereClause(
        string? status, Guid? hubId, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        var conditions = new List<string> { "tp.is_deleted = FALSE" };
        var ps = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("tp.status = @status");
            ps.Add(new NpgsqlParameter("status", status));
        }

        if (hubId.HasValue)
        {
            conditions.Add("t.origin_hub_id = @hubId");
            ps.Add(new NpgsqlParameter("hubId", hubId.Value));
        }

        if (fromDate.HasValue)
        {
            conditions.Add("tp.created_at >= @fromDate");
            ps.Add(new NpgsqlParameter("fromDate", fromDate.Value.ToUniversalTime()));
        }

        if (toDate.HasValue)
        {
            conditions.Add("tp.created_at <= @toDate");
            ps.Add(new NpgsqlParameter("toDate", toDate.Value.ToUniversalTime()));
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        return (where, ps.ToArray());
    }
}
