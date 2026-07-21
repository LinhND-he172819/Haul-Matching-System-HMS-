using HMS.Modules.Transport.Application.DTOs.TripPosts;
using HMS.Modules.Transport.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Transport.Application.Services;

public sealed class TripPostService : ITripPostService
{
    private readonly ITripPostRepository _postRepo;
    private readonly string _connectionString;

    public TripPostService(ITripPostRepository postRepo, IConfiguration configuration)
    {
        _postRepo = postRepo;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    // ── 5.1 Get eligible trips ────────────────────────────────────────

    public async Task<IReadOnlyList<EligibleTripResponse>> GetEligibleTripsAsync(
        Guid currentUserId, string role, Guid? hubId, string? keyword, CancellationToken ct = default)
    {
        // Resolve Hub scope
        Guid? resolvedHubId = role == "Admin" ? hubId : hubId /* ignored for Staff — forced to JWT */;

        // For Staff, we must use the JWT HubId, not what frontend sends
        if (role == "Warehouse_Staff")
        {
            resolvedHubId = await GetStaffHubIdAsync(currentUserId, ct);
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();

        var conditions = new List<string>
        {
            "t.status = 'Active'",
            "t.is_deleted = FALSE",
            "u.is_deleted = FALSE",
            "v.is_deleted = FALSE",
            "oh.is_deleted = FALSE",
            "dh.is_deleted = FALSE",
            "(v.max_weight_kg - t.current_load_weight) > 0",
            "(v.max_volume_cbm - t.current_load_volume) > 0",
            "NOT EXISTS (SELECT 1 FROM transport.trip_posts tp2 WHERE tp2.trip_id = t.id AND tp2.status = 'Open' AND tp2.is_deleted = FALSE)"
        };

        if (resolvedHubId.HasValue)
        {
            conditions.Add("t.origin_hub_id = @hubId");
            cmd.Parameters.AddWithValue("hubId", resolvedHubId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword}%";
            conditions.Add("(oh.name ILIKE @kw OR dh.name ILIKE @kw OR v.license_plate ILIKE @kw OR u.full_name ILIKE @kw)");
            cmd.Parameters.AddWithValue("kw", NpgsqlDbType.Text, kw);
        }

        var whereClause = "WHERE " + string.Join(" AND ", conditions);

        cmd.CommandText = $"""
            SELECT
                t.id AS trip_id,
                t.origin_hub_id, oh.name AS origin_hub_name,
                t.dest_hub_id, dh.name AS dest_hub_name,
                t.driver_id, u.full_name AS driver_name,
                t.vehicle_id, v.license_plate, v.vehicle_type,
                v.max_weight_kg, t.current_load_weight,
                (v.max_weight_kg - t.current_load_weight) AS remaining_weight,
                v.max_volume_cbm, t.current_load_volume,
                (v.max_volume_cbm - t.current_load_volume) AS remaining_volume,
                t.started_at, t.status
            FROM transport.trips t
            JOIN identity.users u ON u.id = t.driver_id AND u.is_deleted = FALSE
            JOIN transport.vehicles v ON v.id = t.vehicle_id AND v.is_deleted = FALSE
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id AND oh.is_deleted = FALSE
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id AND dh.is_deleted = FALSE
            {whereClause}
            ORDER BY t.created_at DESC;
            """;

        var results = new List<EligibleTripResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new EligibleTripResponse(
                TripId: reader.GetGuid(0),
                OriginHubId: reader.GetGuid(1),
                OriginHubName: reader.GetString(2),
                DestinationHubId: reader.GetGuid(3),
                DestinationHubName: reader.GetString(4),
                DriverId: reader.GetGuid(5),
                DriverName: reader.GetString(6),
                VehicleId: reader.GetGuid(7),
                LicensePlate: reader.GetString(8),
                TruckType: reader.GetString(9),
                MaxWeightKg: reader.GetDecimal(10),
                CurrentLoadWeightKg: reader.GetDecimal(11),
                RemainingWeightKg: reader.GetDecimal(12),
                MaxVolumeCbm: reader.GetDecimal(13),
                CurrentLoadVolumeCbm: reader.GetDecimal(14),
                RemainingVolumeCbm: reader.GetDecimal(15),
                StartedAt: reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                Status: reader.GetString(17)
            ));
        }

        return results;
    }

    // ── 5.2 Create post ──────────────────────────────────────────────

    public async Task<TripPostCreateResponse> CreateAsync(
        Guid currentUserId, string role, Guid? jwtHubId, CreateTripPostRequest request, CancellationToken ct = default)
    {
        // 1. Load trip + vehicle + hubs + driver
        var tripInfo = await LoadTripFullAsync(request.TripId, ct);
        if (tripInfo == null) throw new KeyNotFoundException("Chuyến đi không tồn tại.");

        var trip = tripInfo.Trip;
        var vehicle = tripInfo.Vehicle;
        var originHub = tripInfo.OriginHub;
        var destHub = tripInfo.DestHub;

        // 2. Validate status
        if (trip.Status != "Active")
            throw new InvalidOperationException("Chuyến đi phải ở trạng thái Active.");

        // 3. Validate Staff hub scope
        if (role == "Warehouse_Staff")
        {
            if (trip.OriginHubId != jwtHubId)
                throw new UnauthorizedAccessException("Bạn không có quyền đăng bài cho chuyến không thuộc Hub của mình.");
        }

        // 4. Check remaining capacity
        var remainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight;
        var remainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume;

        if (remainingWeight <= 0 || remainingVolume <= 0)
            throw new InvalidOperationException("Chuyến không còn đủ tải trọng hoặc thể tích để đăng nhận hàng.");

        // 5. Check no Open post
        if (await _postRepo.HasOpenPostForTripAsync(request.TripId, ct))
            throw new InvalidOperationException("Chuyến này đã có một bài đăng đang mở.");

        // 6. Generate title
        var title = GenerateTitle(originHub.Name, destHub.Name, vehicle.LicensePlate, remainingWeight, remainingVolume);

        var now = DateTimeOffset.UtcNow;
        var post = new TripPostRecord
        {
            Id = Guid.NewGuid(),
            TripId = request.TripId,
            CreatedBy = currentUserId,
            Title = title,
            Description = request.Description,
            AcceptUntil = request.AcceptUntil,
            Status = "Open",
            PublishedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var postId = await _postRepo.CreatePostAsync(post, ct);

        return new TripPostCreateResponse(
            Id: postId,
            TripId: request.TripId,
            Title: title,
            Status: "Open",
            Message: "Đăng bài chuyến xe thành công.");
    }

    // ── 5.3 Admin/Staff list ─────────────────────────────────────────

    public async Task<PagedTripPostResponse<TripPostListItemResponse>> ListAsync(
        Guid currentUserId, string role, Guid? jwtHubId, TripPostFilterRequest filter, CancellationToken ct = default)
    {
        Guid? resolvedHubId = role == "Admin" ? filter.HubId : jwtHubId;

        var totalCount = await _postRepo.CountPostsAsync(filter.Status, resolvedHubId, filter.FromDate, filter.ToDate, ct);
        var items = await _postRepo.ListPostsAsync(filter, resolvedHubId, ct);

        var totalPages = filter.PageSize > 0
            ? (int)Math.Ceiling((double)totalCount / filter.PageSize) : 0;

        return new PagedTripPostResponse<TripPostListItemResponse>(
            Items: items,
            Page: filter.Page,
            PageSize: filter.PageSize,
            TotalItems: totalCount,
            TotalPages: totalPages);
    }

    // ── 5.4 Detail ───────────────────────────────────────────────────

    public async Task<TripPostDetailResponse?> GetByIdAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancellationToken ct = default)
    {
        var post = await _postRepo.GetPostByIdAsync(id, ct);
        if (post == null) return null;

        var tripInfo = await LoadTripFullAsync(post.TripId, ct);
        if (tripInfo == null) return null;

        var trip = tripInfo.Trip;
        var vehicle = tripInfo.Vehicle;
        var originHub = tripInfo.OriginHub;
        var destHub = tripInfo.DestHub;
        var driver = tripInfo.Driver;

        // Staff hub check
        if (role == "Warehouse_Staff" && trip.OriginHubId != jwtHubId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem bài đăng thuộc Hub khác.");

        // Load creator name
        var creatorName = await GetUserNameAsync(post.CreatedBy, ct);

        var remainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight;
        var remainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume;

        return new TripPostDetailResponse(
            Id: post.Id,
            TripId: post.TripId,
            Title: post.Title,
            Description: post.Description,
            OriginHubId: originHub.Id,
            OriginHubName: originHub.Name,
            DestinationHubId: destHub.Id,
            DestinationHubName: destHub.Name,
            DriverId: driver.Id,
            DriverName: driver.FullName,
            VehicleId: vehicle.Id,
            LicensePlate: vehicle.LicensePlate,
            TruckType: vehicle.VehicleType,
            MaxWeightKg: vehicle.MaxWeightKg,
            MaxVolumeCbm: vehicle.MaxVolumeCbm,
            CurrentLoadWeightKg: trip.CurrentLoadWeight,
            CurrentLoadVolumeCbm: trip.CurrentLoadVolume,
            RemainingWeightKg: remainingWeight,
            RemainingVolumeCbm: remainingVolume,
            TripStartedAt: trip.StartedAt,
            TripStatus: trip.Status,
            Status: post.Status,
            AcceptUntil: post.AcceptUntil,
            PublishedAt: post.PublishedAt,
            ClosedAt: post.ClosedAt,
            CreatedById: post.CreatedBy,
            CreatedByName: creatorName,
            CreatedAt: post.CreatedAt,
            UpdatedAt: post.UpdatedAt
        );
    }

    // ── 5.5 Update ───────────────────────────────────────────────────

    public async Task<TripPostDetailResponse?> UpdateAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, UpdateTripPostRequest request, CancellationToken ct = default)
    {
        var post = await _postRepo.GetPostByIdAsync(id, ct);
        if (post == null) return null;

        if (post.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể cập nhật bài đăng đang mở.");

        var tripInfo = await LoadTripFullAsync(post.TripId, ct);
        if (tripInfo == null) throw new InvalidOperationException("Trip liên kết không tồn tại.");

        var trip = tripInfo.Trip;
        var vehicle = tripInfo.Vehicle;
        var originHub = tripInfo.OriginHub;
        var destHub = tripInfo.DestHub;

        // Staff hub check
        if (role == "Warehouse_Staff" && trip.OriginHubId != jwtHubId)
            throw new UnauthorizedAccessException("Bạn không có quyền cập nhật bài đăng thuộc Hub khác.");

        // Recalculate capacity and regenerate title
        var remainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight;
        var remainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume;

        post.Title = GenerateTitle(originHub.Name, destHub.Name, vehicle.LicensePlate, remainingWeight, remainingVolume);

        if (request.Description is not null)
            post.Description = request.Description;

        if (request.AcceptUntil.HasValue)
        {
            if (request.AcceptUntil.Value <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Hạn nhận đề xuất phải lớn hơn thời điểm hiện tại.");
            post.AcceptUntil = request.AcceptUntil.Value;
        }

        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _postRepo.UpdatePostAsync(post, ct);

        // Reload to return fresh detail
        return await GetByIdAsync(currentUserId, role, jwtHubId, id, ct);
    }

    // ── 5.6 Close ────────────────────────────────────────────────────

    public async Task<bool> CloseAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancellationToken ct = default)
    {
        var post = await _postRepo.GetPostByIdAsync(id, ct);
        if (post == null) return false;

        if (post.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể đóng bài đăng đang mở.");

        // Staff hub check
        var trip = await GetTripByIdAsync(post.TripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip liên kết không tồn tại.");

        if (role == "Warehouse_Staff" && trip.OriginHubId != jwtHubId)
            throw new UnauthorizedAccessException("Bạn không có quyền đóng bài đăng thuộc Hub khác.");

        var now = DateTimeOffset.UtcNow;
        post.Status = "Closed";
        post.ClosedAt = now;
        post.UpdatedAt = now;

        await _postRepo.UpdatePostAsync(post, ct);
        return true;
    }

    // ── 5.7 Cancel ───────────────────────────────────────────────────

    public async Task<bool> CancelAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancelTripPostRequest? request, CancellationToken ct = default)
    {
        var post = await _postRepo.GetPostByIdAsync(id, ct);
        if (post == null) return false;

        if (post.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể hủy bài đăng đang mở.");

        // Staff hub check
        var trip = await GetTripByIdAsync(post.TripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip liên kết không tồn tại.");

        if (role == "Warehouse_Staff" && trip.OriginHubId != jwtHubId)
            throw new UnauthorizedAccessException("Bạn không có quyền hủy bài đăng thuộc Hub khác.");

        var now = DateTimeOffset.UtcNow;
        post.Status = "Cancelled";
        post.ClosedAt = now;
        post.UpdatedAt = now;

        await _postRepo.UpdatePostAsync(post, ct);
        return true;
    }

    // ── 5.8 Public list ──────────────────────────────────────────────

    public async Task<PagedTripPostResponse<PublicTripPostResponse>> ListPublicAsync(
        PublicTripPostFilterRequest filter, CancellationToken ct = default)
    {
        var items = await _postRepo.ListPublicPostsAsync(filter, ct);

        return new PagedTripPostResponse<PublicTripPostResponse>(
            Items: items,
            Page: filter.Page,
            PageSize: filter.PageSize,
            TotalItems: items.Count,
            TotalPages: filter.PageSize > 0 ? (int)Math.Ceiling((double)items.Count / filter.PageSize) : 0);
    }

    // ── KPI ──────────────────────────────────────────────────────────

    public async Task<TripPostKpiResponse> GetKpiAsync(
        Guid currentUserId, string role, Guid? jwtHubId, CancellationToken ct = default)
    {
        Guid? hubId = role == "Admin" ? null : jwtHubId;
        var kpi = await _postRepo.GetKpiAsync(hubId, ct);

        // Count eligible trips separately
        var eligibleTrips = await GetEligibleTripsAsync(currentUserId, role, hubId, null, ct);

        return kpi with { EligibleTrips = eligibleTrips.Count };
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static string GenerateTitle(
        string originHubName, string destHubName, string licensePlate,
        decimal remainingWeightKg, decimal remainingVolumeCbm)
    {
        return $"{originHubName} → {destHubName} | Xe {licensePlate} | Còn {remainingWeightKg:N0} kg • {remainingVolumeCbm:N1} CBM";
    }

    private async Task<Guid?> GetStaffHubIdAsync(Guid userId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT hub_id FROM identity.users WHERE id = @userId AND is_deleted = FALSE;";
        cmd.Parameters.AddWithValue("userId", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as Guid?;
    }

    private async Task<string> GetUserNameAsync(Guid userId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT full_name FROM identity.users WHERE id = @userId;";
        cmd.Parameters.AddWithValue("userId", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString() ?? "N/A";
    }

    private async Task<TripInfo?> LoadTripFullAsync(Guid tripId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                t.id, t.driver_id, t.vehicle_id, t.origin_hub_id, t.dest_hub_id,
                t.current_load_weight, t.current_load_volume, t.started_at, t.status,
                v.id, v.license_plate, v.vehicle_type, v.max_weight_kg, v.max_volume_cbm,
                oh.id, oh.name,
                dh.id, dh.name,
                u.id, u.full_name
            FROM transport.trips t
            JOIN transport.vehicles v ON v.id = t.vehicle_id
            JOIN identity.hubs oh ON oh.id = t.origin_hub_id
            JOIN identity.hubs dh ON dh.id = t.dest_hub_id
            JOIN identity.users u ON u.id = t.driver_id
            WHERE t.id = @tripId AND t.is_deleted = FALSE;
            """;
        cmd.Parameters.AddWithValue("tripId", tripId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new TripInfo
        {
            Trip = new TripData
            {
                Id = reader.GetGuid(0),
                DriverId = reader.GetGuid(1),
                VehicleId = reader.GetGuid(2),
                OriginHubId = reader.GetGuid(3),
                DestHubId = reader.GetGuid(4),
                CurrentLoadWeight = reader.GetDecimal(5),
                CurrentLoadVolume = reader.GetDecimal(6),
                StartedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                Status = reader.GetString(8),
            },
            Vehicle = new VehicleData
            {
                Id = reader.GetGuid(9),
                LicensePlate = reader.GetString(10),
                VehicleType = reader.GetString(11),
                MaxWeightKg = reader.GetDecimal(12),
                MaxVolumeCbm = reader.GetDecimal(13),
            },
            OriginHub = new HubData { Id = reader.GetGuid(14), Name = reader.GetString(15) },
            DestHub = new HubData { Id = reader.GetGuid(16), Name = reader.GetString(17) },
            Driver = new DriverData { Id = reader.GetGuid(18), FullName = reader.GetString(19) },
        };
    }

    private async Task<TripData?> GetTripByIdAsync(Guid tripId, CancellationToken ct)
    {
        var full = await LoadTripFullAsync(tripId, ct);
        return full?.Trip;
    }

    // ── Inner DTOs ───────────────────────────────────────────────────

    private sealed class TripInfo
    {
        public TripData Trip { get; set; } = null!;
        public VehicleData Vehicle { get; set; } = null!;
        public HubData OriginHub { get; set; } = null!;
        public HubData DestHub { get; set; } = null!;
        public DriverData Driver { get; set; } = null!;
    }

    private sealed class TripData
    {
        public Guid Id { get; set; }
        public Guid DriverId { get; set; }
        public Guid VehicleId { get; set; }
        public Guid OriginHubId { get; set; }
        public Guid DestHubId { get; set; }
        public decimal CurrentLoadWeight { get; set; }
        public decimal CurrentLoadVolume { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public string Status { get; set; } = null!;
    }

    private sealed class VehicleData
    {
        public Guid Id { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string VehicleType { get; set; } = null!;
        public decimal MaxWeightKg { get; set; }
        public decimal MaxVolumeCbm { get; set; }
    }

    private sealed class HubData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private sealed class DriverData
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
    }
}
