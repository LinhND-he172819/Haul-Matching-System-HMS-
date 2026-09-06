using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Transport.Application.Services;

public sealed class TripService(
    ITripRepository repository,
    ITripRoutePlanner routePlanner,
    IVehicleRepository vehicleRepository,
    IShipmentStateService shipmentStateService,
    IConfiguration configuration,
    ILogger<TripService> logger) : ITripService
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    private readonly ILogger<TripService> _logger = logger;
    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default)
    {
        const int maxActiveTrips = 2;

        var driverActiveCount = await repository.CountActiveTripsForDriverAsync(request.DriverId, excludeTripId: null, cancellationToken);
        if (driverActiveCount >= maxActiveTrips)
        {
            throw new InvalidOperationException(
                $"Tài xế này đã có {driverActiveCount} chuyến đang hoạt động (tối đa {maxActiveTrips}). Vui lòng hoàn thành hoặc hủy chuyến trước.");
        }

        var vehicleActiveCount = await repository.CountActiveTripsForVehicleAsync(request.VehicleId, excludeTripId: null, cancellationToken);
        if (vehicleActiveCount >= maxActiveTrips)
        {
            throw new InvalidOperationException(
                $"Xe này đã có {vehicleActiveCount} chuyến đang hoạt động (tối đa {maxActiveTrips}). Vui lòng chọn xe khác.");
        }

        var scheduledDepartureAt = ValidateScheduledDepartureAt(request.ScheduledDepartureAt);

        var vehicle = await GetVehicleOrThrowAsync(request.VehicleId, cancellationToken);

        // Warehouse shipments to link (optional). Loads are validated against vehicle capacity.
        var shipmentsToLink = await LoadWarehouseShipmentsAsync(
            request.WarehouseShipmentIds, request.OriginHubId, cancellationToken);

        var totalWeight = request.CurrentLoadWeightKg + shipmentsToLink.Sum(s => s.WeightKg);
        var totalVolume = request.CurrentLoadVolumeCbm + shipmentsToLink.Sum(s => s.VolumeCbm);

        ValidateLoadCapacity(vehicle, totalWeight, totalVolume);

        var routeLineString = await routePlanner.ResolveRouteLineStringAsync(
            request.OriginHubId,
            request.DestHubId,
            request.RouteLineString,
            cancellationToken);

        var trip = Trip.Create(
            request.DriverId,
            request.VehicleId,
            request.OriginHubId,
            request.DestHubId,
            routeLineString,
            request.CurrentLoadWeightKg,
            request.CurrentLoadVolumeCbm,
            scheduledDepartureAt);

        if (shipmentsToLink.Count > 0)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await repository.AddAsync(trip, connection, transaction, cancellationToken);
                await LinkShipmentsAsync(trip, shipmentsToLink, connection, transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        else
        {
            await repository.AddAsync(trip, cancellationToken);
        }

        return ToResponse(trip);
    }

    public async Task<IReadOnlyCollection<TripResponse>> ListAsync(
        Guid? driverId,
        TripStatus? status,
        CancellationToken cancellationToken = default)
    {
        var trips = await repository.ListAsync(driverId, status, cancellationToken);

        return trips
            .OrderBy(trip => trip.CreatedAt)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<TripResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    public async Task<TripResponse?> UpdateAsync(
        Guid id,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        const int maxActiveTrips = 2;

        var driverActiveCount = await repository.CountActiveTripsForDriverAsync(request.DriverId, excludeTripId: id, cancellationToken);
        if (driverActiveCount >= maxActiveTrips)
        {
            throw new InvalidOperationException(
                $"Tài xế này đã có {driverActiveCount} chuyến đang hoạt động khác (tối đa {maxActiveTrips}). Vui lòng hoàn thành hoặc hủy chuyến trước.");
        }

        var vehicleActiveCount = await repository.CountActiveTripsForVehicleAsync(request.VehicleId, excludeTripId: id, cancellationToken);
        if (vehicleActiveCount >= maxActiveTrips)
        {
            throw new InvalidOperationException(
                $"Xe này đã có {vehicleActiveCount} chuyến đang hoạt động khác (tối đa {maxActiveTrips}). Vui lòng chọn xe khác.");
        }

        var scheduledDepartureAt = ValidateScheduledDepartureAt(request.ScheduledDepartureAt);

        var vehicle = await GetVehicleOrThrowAsync(request.VehicleId, cancellationToken);

        // Current warehouse-linked shipments on this trip (status Matched, not deleted).
        var currentLinks = await LoadActiveTripShipmentLinksAsync(trip.Id, cancellationToken);

        var requestedIds = (request.WarehouseShipmentIds ?? new List<Guid>())
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        // Duplicate booking guard: a shipment must not be on another active trip.
        if (requestedIds.Count > 0)
        {
            var conflicts = await FindShipmentsOnOtherActiveTripsAsync(requestedIds, trip.Id, cancellationToken);
            if (conflicts.Count > 0)
            {
                throw new InvalidOperationException(
                    "Một số đơn hàng đã được xếp lên chuyến khác đang hoạt động: " +
                    string.Join(", ", conflicts.Select(c => c.QrCode)) + ".");
            }
        }

        var toRemove = currentLinks
            .Where(l => !requestedIds.Contains(l.ShipmentId))
            .ToList();

        var existingIds = currentLinks.Select(l => l.ShipmentId).ToHashSet();
        var toAddIds = requestedIds.Where(g => !existingIds.Contains(g)).ToList();
        var shipmentsToAdd = await LoadWarehouseShipmentsAsync(toAddIds, request.OriginHubId, cancellationToken);

        // Loads: base request loads + kept links, new links add their loads.
        var keptWeight = currentLinks
            .Where(l => !toRemove.Any(r => r.ShipmentId == l.ShipmentId))
            .Sum(l => l.WeightKg);
        var keptVolume = currentLinks
            .Where(l => !toRemove.Any(r => r.ShipmentId == l.ShipmentId))
            .Sum(l => l.VolumeCbm);

        var totalWeight = request.CurrentLoadWeightKg + keptWeight + shipmentsToAdd.Sum(s => s.WeightKg);
        var totalVolume = request.CurrentLoadVolumeCbm + keptVolume + shipmentsToAdd.Sum(s => s.VolumeCbm);

        ValidateLoadCapacity(vehicle, totalWeight, totalVolume);

        var routeLineString = await routePlanner.ResolveRouteLineStringAsync(
            request.OriginHubId,
            request.DestHubId,
            request.RouteLineString,
            cancellationToken);

        trip.UpdateDetails(
            request.DriverId,
            request.VehicleId,
            request.OriginHubId,
            request.DestHubId,
            routeLineString,
            request.CurrentLoadWeightKg,
            request.CurrentLoadVolumeCbm,
            scheduledDepartureAt);

        var hasLinkChanges = toRemove.Count > 0 || shipmentsToAdd.Count > 0;

        if (hasLinkChanges)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await repository.UpdateAsync(trip, connection, transaction, cancellationToken);

                foreach (var removed in toRemove)
                {
                    await UnlinkShipmentAsync(trip, removed, connection, transaction, cancellationToken);
                }

                await LinkShipmentsAsync(trip, shipmentsToAdd, connection, transaction, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        else
        {
            await repository.UpdateAsync(trip, cancellationToken);
        }

        return ToResponse(trip);
    }

    public async Task<TripResponse?> ChangeStatusAsync(
        Guid id,
        ChangeTripStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        // Validate: only 1 InProgress trip allowed per driver and per vehicle
        if (request.Status == TripStatus.InProgress)
        {
            var driverInProgressCount = await repository.CountInProgressTripsForDriverAsync(
                trip.DriverId, excludeTripId: id, cancellationToken);
            if (driverInProgressCount > 0)
            {
                throw new InvalidOperationException(
                    $"Tài xế này đã có {driverInProgressCount} chuyến đang thực hiện (InProgress). " +
                    "Vui lòng hoàn thành chuyến hiện tại trước khi bắt đầu chuyến mới.");
            }

            var vehicleInProgressCount = await repository.CountInProgressTripsForVehicleAsync(
                trip.VehicleId, excludeTripId: id, cancellationToken);
            if (vehicleInProgressCount > 0)
            {
                throw new InvalidOperationException(
                    $"Xe này đã có {vehicleInProgressCount} chuyến đang thực hiện (InProgress). " +
                    "Vui lòng hoàn thành chuyến hiện tại trước khi bắt đầu chuyến mới.");
            }
        }

        // Validate: when completing a trip, all linked shipments must be Delivered.
        if (request.Status == TripStatus.Completed)
        {
            var notDelivered = await GetNotDeliveredShipmentQrCodesAsync(id, cancellationToken);
            if (notDelivered.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Không thể hoàn thành chuyến. Còn {notDelivered.Count} đơn chưa giao thành công: " +
                    string.Join(", ", notDelivered) + ".");
            }
        }

        trip.ChangeStatus(request.Status, request.OccurredAt ?? DateTimeOffset.UtcNow);

        // When the trip is cancelled, auto-unlink any shipments still in 'Matched' status
        // (i.e. not yet picked up by the driver) and revert them to In_Warehouse so they
        // can be re-assigned to another trip. In_Transit shipments are left as-is — they
        // must be resolved via the delivery workflow (Delivery_Failed → Returned_To_Hub).
        if (request.Status == TripStatus.Cancelled)
        {
            var autoUnlinkedOnCancel = await UnlinkMatchedShipmentsOnCancelAsync(
                trip, cancellationToken);
            if (autoUnlinkedOnCancel.Count > 0)
            {
                _logger.LogInformation(
                    "Trip {TripCode} cancelled: auto-unlinked {Count} matched shipments: {QrCodes}",
                    trip.TripCode, autoUnlinkedOnCancel.Count, string.Join(", ", autoUnlinkedOnCancel));
            }
        }

        await repository.UpdateAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    /// <summary>
    /// Admin endpoint: list all shipments (any link status) currently linked to a trip.
    /// Returns null when the trip does not exist.
    /// </summary>
    public async Task<IReadOnlyList<TripShipmentResponse>?> GetTripShipmentsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT s.id, s.qr_code, s.cargo_type, s.weight_kg, s.volume_cbm, s.status,
                   s.sender_name, s.sender_phone, s.pickup_address,
                   s.receiver_name, s.receiver_phone, s.dest_address
            FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id AND s.is_deleted = FALSE
            WHERE ts.trip_id = @trip_id AND ts.is_deleted = FALSE
            ORDER BY ts.delivery_sequence ASC NULLS LAST, ts.suggested_at ASC;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("trip_id", tripId);

        var items = new List<TripShipmentResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TripShipmentResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        return items;
    }

    /// <summary>
    /// Admin endpoint: unlink a single shipment from a trip.
    /// Use case: vehicle breakdown → operator removes some shipments to reassign them to another trip.
    /// Only shipments in 'Matched' status (not yet picked up) can be unlinked this way.
    /// </summary>
    public async Task<UnlinkTripShipmentResult?> UnlinkTripShipmentAsync(
        Guid tripId,
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        // Only non-terminal, non-Completed trips can have shipments removed.
        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Không thể gỡ đơn hàng khỏi chuyến đã {trip.Status}.");
        }

        // Find the link + verify the shipment is in 'Matched' status (not yet picked up).
        var currentLinks = await LoadActiveTripShipmentLinksAsync(tripId, cancellationToken);
        var link = currentLinks.FirstOrDefault(l => l.ShipmentId == shipmentId);
        if (link is null)
        {
            // Either not linked, or already in In_Transit (cannot be unlinked here).
            throw new InvalidOperationException(
                "Đơn hàng không thuộc chuyến này hoặc đã được vận chuyển (In_Transit). " +
                "Đơn đang vận chuyển cần xử lý qua quy trình giao hàng (Delivery_Failed → Returned).");
        }

        var unlinkedQr = link.QrCode;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await UnlinkShipmentAsync(trip, link, connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        // Reload trip to pick up updated load + version from UnlinkShipmentAsync.
        var refreshed = await repository.GetByIdAsync(tripId, cancellationToken)
            ?? trip;

        return new UnlinkTripShipmentResult(
            ToResponse(refreshed),
            unlinkedQr,
            AlsoUnlinkedOnCancel: Array.Empty<string>());
    }

    /// <summary>
    /// Unlinks every Matched-status shipment from a cancelled trip and reverts
    /// each shipment to In_Warehouse. In_Transit shipments are left alone — they
    /// require the delivery workflow to mark them Delivery_Failed first.
    /// </summary>
    private async Task<List<string>> UnlinkMatchedShipmentsOnCancelAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var links = await LoadActiveTripShipmentLinksAsync(trip.Id, cancellationToken);
        if (links.Count == 0)
        {
            return new List<string>();
        }

        var unlinked = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var link in links)
            {
                await UnlinkShipmentAsync(trip, link, connection, transaction, cancellationToken);
                unlinked.Add(link.QrCode);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return unlinked;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    // ── Validation helpers ────────────────────────────────────────────

    private static DateTimeOffset ValidateScheduledDepartureAt(DateTimeOffset? value)
    {
        if (value is null || value.Value == default)
        {
            throw new ArgumentException("Ngày khởi hành dự kiến là bắt buộc.", nameof(value));
        }

        if (value.Value <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Ngày khởi hành dự kiến phải lớn hơn thời điểm hiện tại.", nameof(value));
        }

        return value.Value;
    }

    private async Task<Vehicle> GetVehicleOrThrowAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            throw new ArgumentException("Xe không tồn tại.", nameof(vehicleId));
        }

        return vehicle;
    }

    private static void ValidateLoadCapacity(Vehicle vehicle, decimal weightKg, decimal volumeCbm)
    {
        if (weightKg > vehicle.MaxWeightKg)
        {
            throw new ArgumentException(
                $"Tải trọng {weightKg:N0} kg vượt quá tải trọng tối đa {vehicle.MaxWeightKg:N0} kg của xe {vehicle.LicensePlate}.",
                nameof(weightKg));
        }

        if (volumeCbm > vehicle.MaxVolumeCbm)
        {
            throw new ArgumentException(
                $"Dung tích {volumeCbm:N0} CBM vượt quá dung tích tối đa {vehicle.MaxVolumeCbm:N0} CBM của xe {vehicle.LicensePlate}.",
                nameof(volumeCbm));
        }
    }

    // ── Warehouse shipment loading ────────────────────────────────────

    private sealed record WarehouseShipmentInfo(Guid ShipmentId, string QrCode, decimal WeightKg, decimal VolumeCbm);

    private async Task<List<WarehouseShipmentInfo>> LoadWarehouseShipmentsAsync(
        List<Guid>? shipmentIds,
        Guid originHubId,
        CancellationToken cancellationToken)
    {
        var ids = (shipmentIds ?? new List<Guid>())
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new List<WarehouseShipmentInfo>();
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await LoadWarehouseShipmentsCoreAsync(ids, originHubId, connection, cancellationToken);
    }

    private async Task<List<WarehouseShipmentInfo>> LoadWarehouseShipmentsCoreAsync(
        List<Guid> ids,
        Guid originHubId,
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var result = new List<WarehouseShipmentInfo>();

        foreach (var id in ids)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, qr_code, weight_kg, volume_cbm, status, current_hub_id
                FROM warehouse.shipments
                WHERE id = @id AND is_deleted = FALSE
                FOR UPDATE;
                """;
            command.Parameters.AddWithValue("id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException($"Đơn hàng {id} không tồn tại.", nameof(ids));
            }

            var status = reader.GetString(reader.GetOrdinal("status"));
            var currentHubId = reader.IsDBNull(reader.GetOrdinal("current_hub_id"))
                ? (Guid?)null
                : reader.GetGuid(reader.GetOrdinal("current_hub_id"));

            // Must be sitting in the origin hub warehouse (In_Warehouse or Returned).
            var inOriginWarehouse =
                (status is "In_Warehouse" or "Returned") && currentHubId == originHubId;

            if (!inOriginWarehouse)
            {
                throw new InvalidOperationException(
                    $"Đơn hàng {reader.GetString(reader.GetOrdinal("qr_code"))} không nằm trong kho của hub xuất phát (trạng thái: {status}).");
            }

            result.Add(new WarehouseShipmentInfo(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("qr_code")),
                reader.GetDecimal(reader.GetOrdinal("weight_kg")),
                reader.GetDecimal(reader.GetOrdinal("volume_cbm"))));
        }

        return result;
    }

    private sealed record TripShipmentLink(Guid ShipmentId, string QrCode, decimal WeightKg, decimal VolumeCbm);

    private async Task<List<TripShipmentLink>> LoadActiveTripShipmentLinksAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await LoadActiveTripShipmentLinksCoreAsync(tripId, connection, cancellationToken);
    }

    private async Task<List<TripShipmentLink>> LoadActiveTripShipmentLinksCoreAsync(
        Guid tripId,
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var links = new List<TripShipmentLink>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ts.shipment_id, s.qr_code, s.weight_kg, s.volume_cbm
            FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id
            WHERE ts.trip_id = @trip_id
                AND ts.status = 'Matched'
                AND ts.is_deleted = FALSE
                AND s.is_deleted = FALSE;
            """;
        command.Parameters.AddWithValue("trip_id", tripId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(new TripShipmentLink(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3)));
        }

        return links;
    }

    private sealed record ShipmentConflict(Guid ShipmentId, string QrCode);

    private async Task<List<ShipmentConflict>> FindShipmentsOnOtherActiveTripsAsync(
        List<Guid> shipmentIds,
        Guid excludeTripId,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<ShipmentConflict>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var shipmentId in shipmentIds)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.qr_code
                FROM transport.trip_shipments ts
                JOIN transport.trips t ON t.id = ts.trip_id
                JOIN warehouse.shipments s ON s.id = ts.shipment_id
                WHERE ts.shipment_id = @shipment_id
                    AND ts.trip_id <> @exclude_trip_id
                    AND ts.status IN ('Suggested', 'Matched', 'In_Transit')
                    AND ts.is_deleted = FALSE
                    AND t.is_deleted = FALSE
                    AND t.status IN ('Active', 'Scheduled', 'Ready', 'InProgress')
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("shipment_id", shipmentId);
            command.Parameters.AddWithValue("exclude_trip_id", excludeTripId);

            var qrCode = await command.ExecuteScalarAsync(cancellationToken);
            if (qrCode is string code)
            {
                conflicts.Add(new ShipmentConflict(shipmentId, code));
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Returns QR codes of linked shipments that are NOT yet Delivered for the given trip.
    /// Used to block completion until every shipment has been delivered.
    /// </summary>
    private async Task<List<string>> GetNotDeliveredShipmentQrCodesAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var notDelivered = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.qr_code
            FROM transport.trip_shipments ts
            JOIN warehouse.shipments s ON s.id = ts.shipment_id
            WHERE ts.trip_id = @trip_id
                AND ts.is_deleted = FALSE
                AND s.is_deleted = FALSE
                AND s.status <> 'Delivered';
            """;
        command.Parameters.AddWithValue("trip_id", tripId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notDelivered.Add(reader.GetString(0));
        }

        return notDelivered;
    }

    // ── Link / unlink (transactional) ─────────────────────────────────

    private async Task LinkShipmentsAsync(
        Trip trip,
        List<WarehouseShipmentInfo> shipments,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var shipment in shipments)
        {
            // In_Warehouse/Returned → Matched via central state service (joins this transaction).
            await shipmentStateService.TransitionAsync(
                shipment.ShipmentId,
                ShipmentStatus.Matched,
                connection: connection,
                transaction: transaction,
                performedBy: null,
                reason: "Linked to trip " + trip.TripCode,
                ct: cancellationToken);

            // Insert trip_shipments row (status Matched). The partial unique index
            // ux_transport_active_trip_shipment is a DB backstop against double-booking.
            const string insertSql = """
                INSERT INTO transport.trip_shipments
                    (id, trip_id, shipment_id, delivery_sequence, status, suggested_at, accepted_at)
                SELECT
                    gen_random_uuid(), @trip_id, @shipment_id,
                    COALESCE((SELECT MAX(delivery_sequence) + 1 FROM transport.trip_shipments WHERE trip_id = @trip_id AND is_deleted = FALSE), 1),
                    'Matched', NOW(), NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM transport.trip_shipments
                    WHERE trip_id = @trip_id AND shipment_id = @shipment_id AND is_deleted = FALSE
                );
                """;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = insertSql;
                command.Parameters.AddWithValue("trip_id", trip.Id);
                command.Parameters.AddWithValue("shipment_id", shipment.ShipmentId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // Add shipment loads to trip.
            await AddTripLoadAsync(trip.Id, shipment.WeightKg, shipment.VolumeCbm, connection, transaction, cancellationToken);
        }
    }

    private async Task UnlinkShipmentAsync(
        Trip trip,
        TripShipmentLink link,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        // Soft-delete the linkage row.
        const string deleteSql = """
            UPDATE transport.trip_shipments
            SET is_deleted = TRUE, updated_at = NOW()
            WHERE trip_id = @trip_id AND shipment_id = @shipment_id
                AND status = 'Matched' AND is_deleted = FALSE;
            """;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = deleteSql;
            command.Parameters.AddWithValue("trip_id", trip.Id);
            command.Parameters.AddWithValue("shipment_id", link.ShipmentId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Matched → In_Warehouse via central state service (joins this transaction).
        await shipmentStateService.TransitionAsync(
            link.ShipmentId,
            ShipmentStatus.In_Warehouse,
            connection: connection,
            transaction: transaction,
            performedBy: null,
            reason: "Unlinked from trip " + trip.TripCode,
            ct: cancellationToken);

        // Subtract shipment loads from trip (floor at zero).
        const string subtractSql = """
            UPDATE transport.trips
            SET current_load_weight = GREATEST(current_load_weight - @weight, 0),
                current_load_volume = GREATEST(current_load_volume - @volume, 0),
                updated_at = NOW()
            WHERE id = @trip_id;
            """;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = subtractSql;
            command.Parameters.AddWithValue("trip_id", trip.Id);
            command.Parameters.AddWithValue("weight", link.WeightKg);
            command.Parameters.AddWithValue("volume", link.VolumeCbm);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task AddTripLoadAsync(
        Guid tripId,
        decimal weightKg,
        decimal volumeCbm,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE transport.trips
            SET current_load_weight = current_load_weight + @weight,
                current_load_volume = current_load_volume + @volume,
                updated_at = NOW()
            WHERE id = @trip_id;
            """;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("trip_id", tripId);
        command.Parameters.AddWithValue("weight", weightKg);
        command.Parameters.AddWithValue("volume", volumeCbm);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TripResponse ToResponse(Trip trip)
    {
        return new TripResponse(
            trip.Id,
            trip.DriverId,
            trip.VehicleId,
            trip.OriginHubId,
            trip.DestHubId,
            trip.RouteLineString,
            trip.CurrentLoadWeightKg,
            trip.CurrentLoadVolumeCbm,
            trip.StartedAt,
            trip.FinishedAt,
            trip.ScheduledDepartureAt,
            trip.Version,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt);
    }
}
