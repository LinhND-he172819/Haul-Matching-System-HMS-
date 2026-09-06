using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Shared.Core.Enums;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Transport.Infrastructure.Repositories;

public sealed class PostgresTripRepository : ITripRepository
{
    private readonly string _connectionString;

    public PostgresTripRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await AddCoreAsync(trip, connection, null, cancellationToken);
    }

    public async Task AddAsync(Trip trip, object? connection, object? transaction, CancellationToken cancellationToken = default)
    {
        var (conn, txn, ownsConnection) = await ResolveConnectionAsync(connection, transaction, cancellationToken);
        try
        {
            await AddCoreAsync(trip, conn, txn, cancellationToken);
        }
        finally
        {
            if (ownsConnection && conn is not null)
                await conn.DisposeAsync();
        }
    }

    private async Task AddCoreAsync(Trip trip, NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (transaction is not null)
            command.Transaction = transaction;

        command.CommandText = """
            INSERT INTO transport.trips (
                id,
                trip_code,
                driver_id,
                vehicle_id,
                origin_hub_id,
                dest_hub_id,
                route_linestring,
                current_load_weight,
                current_load_volume,
                scheduled_departure_at,
                started_at,
                finished_at,
                version,
                status,
                created_at,
                updated_at,
                is_deleted
            )
            VALUES (
                @id,
                @trip_code,
                @driver_id,
                @vehicle_id,
                @origin_hub_id,
                @dest_hub_id,
                ST_GeomFromText(@route_linestring, 4326),
                @current_load_weight,
                @current_load_volume,
                @scheduled_departure_at,
                @started_at,
                @finished_at,
                @version,
                @status,
                @created_at,
                @updated_at,
                FALSE
            );
            """;

        AddTripParameters(command, trip);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Trip>> ListAsync(
        Guid? driverId,
        TripStatus? status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var filters = new List<string> { "is_deleted = FALSE" };
        if (driverId.HasValue)
        {
            filters.Add("driver_id = @driver_id");
            command.Parameters.AddWithValue("driver_id", driverId.Value);
        }

        if (status.HasValue)
        {
            filters.Add("status = @status");
            command.Parameters.AddWithValue("status", status.Value.ToString());
        }

        command.CommandText = $"""
            {SelectTripSql}
            WHERE {string.Join(" AND ", filters)}
            ORDER BY created_at DESC;
            """;

        var trips = new List<Trip>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            trips.Add(ReadTrip(reader));
        }

        return trips;
    }

    public async Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
            {SelectTripSql}
            WHERE id = @id AND is_deleted = FALSE;
            """;
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadTrip(reader) : null;
    }

    public async Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await UpdateCoreAsync(trip, connection, null, cancellationToken);
    }

    public async Task UpdateAsync(Trip trip, object? connection, object? transaction, CancellationToken cancellationToken = default)
    {
        var (conn, txn, ownsConnection) = await ResolveConnectionAsync(connection, transaction, cancellationToken);
        try
        {
            await UpdateCoreAsync(trip, conn, txn, cancellationToken);
        }
        finally
        {
            if (ownsConnection && conn is not null)
                await conn.DisposeAsync();
        }
    }

    private async Task UpdateCoreAsync(Trip trip, NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (transaction is not null)
            command.Transaction = transaction;

        command.CommandText = """
            UPDATE transport.trips
            SET
                driver_id = @driver_id,
                vehicle_id = @vehicle_id,
                origin_hub_id = @origin_hub_id,
                dest_hub_id = @dest_hub_id,
                route_linestring = ST_GeomFromText(@route_linestring, 4326),
                current_load_weight = @current_load_weight,
                current_load_volume = @current_load_volume,
                scheduled_departure_at = @scheduled_departure_at,
                started_at = @started_at,
                finished_at = @finished_at,
                version = @version,
                status = @status,
                updated_at = @updated_at
            WHERE id = @id AND is_deleted = FALSE;
            """;

        AddTripParameters(command, trip);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE transport.trips
            SET is_deleted = TRUE, updated_at = @updated_at
            WHERE id = @id AND is_deleted = FALSE;
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows > 0;
    }
    public async Task<int> CountActiveTripsForDriverAsync(Guid driverId, Guid? excludeTripId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*) FROM transport.trips
            WHERE driver_id = @driver_id
                AND status IN ('Active', 'Scheduled', 'Ready')
                AND is_deleted = FALSE
                AND (@exclude_trip_id::uuid IS NULL OR id <> @exclude_trip_id);
            """;
        command.Parameters.AddWithValue("driver_id", driverId);
        command.Parameters.AddWithValue("exclude_trip_id",
            excludeTripId.HasValue ? excludeTripId.Value : (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> CountActiveTripsForVehicleAsync(Guid vehicleId, Guid? excludeTripId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*) FROM transport.trips
            WHERE vehicle_id = @vehicle_id
                AND status IN ('Active', 'Scheduled', 'Ready')
                AND is_deleted = FALSE
                AND (@exclude_trip_id::uuid IS NULL OR id <> @exclude_trip_id);
            """;
        command.Parameters.AddWithValue("vehicle_id", vehicleId);
        command.Parameters.AddWithValue("exclude_trip_id",
            excludeTripId.HasValue ? excludeTripId.Value : (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> CountInProgressTripsForDriverAsync(Guid driverId, Guid? excludeTripId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*) FROM transport.trips
            WHERE driver_id = @driver_id
                AND status = 'InProgress'
                AND is_deleted = FALSE
                AND (@exclude_trip_id::uuid IS NULL OR id <> @exclude_trip_id);
            """;
        command.Parameters.AddWithValue("driver_id", driverId);
        command.Parameters.AddWithValue("exclude_trip_id",
            excludeTripId.HasValue ? excludeTripId.Value : (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> CountInProgressTripsForVehicleAsync(Guid vehicleId, Guid? excludeTripId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*) FROM transport.trips
            WHERE vehicle_id = @vehicle_id
                AND status = 'InProgress'
                AND is_deleted = FALSE
                AND (@exclude_trip_id::uuid IS NULL OR id <> @exclude_trip_id);
            """;
        command.Parameters.AddWithValue("vehicle_id", vehicleId);
        command.Parameters.AddWithValue("exclude_trip_id",
            excludeTripId.HasValue ? excludeTripId.Value : (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    /// <summary>
    /// Resolves a caller-provided connection/transaction pair, or opens a new connection.
    /// Returns the connection to use, the transaction to attach (may be null), and whether
    /// this method owns the connection (and therefore must dispose it).
    /// </summary>
    private async Task<(NpgsqlConnection Connection, NpgsqlTransaction? Transaction, bool OwnsConnection)> ResolveConnectionAsync(
        object? connection,
        object? transaction,
        CancellationToken cancellationToken)
    {
        if (connection is NpgsqlConnection callerConn && callerConn.State == System.Data.ConnectionState.Open)
        {
            var callerTxn = transaction as NpgsqlTransaction;
            return (callerConn, callerTxn, false);
        }

        var conn = await OpenConnectionAsync(cancellationToken);
        return (conn, null, true);
    }

    private static void AddTripParameters(NpgsqlCommand command, Trip trip)
    {
        command.Parameters.AddWithValue("id", trip.Id);
        command.Parameters.AddWithValue("trip_code", trip.TripCode ?? $"TRIP-{trip.Id.ToString()[..8]}");
        command.Parameters.AddWithValue("driver_id", trip.DriverId);
        command.Parameters.AddWithValue("vehicle_id", trip.VehicleId);
        command.Parameters.AddWithValue("origin_hub_id", trip.OriginHubId);
        command.Parameters.AddWithValue("dest_hub_id", trip.DestHubId);
        command.Parameters.Add("route_linestring", NpgsqlDbType.Text).Value = trip.RouteLineString;
        command.Parameters.AddWithValue("current_load_weight", trip.CurrentLoadWeightKg);
        command.Parameters.AddWithValue("current_load_volume", trip.CurrentLoadVolumeCbm);
        AddTimestampParameter(command, "scheduled_departure_at", trip.ScheduledDepartureAt);
        AddTimestampParameter(command, "started_at", trip.StartedAt);
        AddTimestampParameter(command, "finished_at", trip.FinishedAt);
        command.Parameters.AddWithValue("version", trip.Version);
        command.Parameters.AddWithValue("status", trip.Status.ToString());
        command.Parameters.AddWithValue("created_at", trip.CreatedAt.ToUniversalTime());
        command.Parameters.AddWithValue("updated_at", trip.UpdatedAt.ToUniversalTime());
    }

    private static void AddTimestampParameter(NpgsqlCommand command, string name, DateTimeOffset? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.TimestampTz).Value =
            value.HasValue ? value.Value.ToUniversalTime() : DBNull.Value;
    }

    private static Trip ReadTrip(NpgsqlDataReader reader)
    {
        return Trip.Rehydrate(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("driver_id")),
            reader.GetGuid(reader.GetOrdinal("vehicle_id")),
            reader.GetGuid(reader.GetOrdinal("origin_hub_id")),
            reader.GetGuid(reader.GetOrdinal("dest_hub_id")),
            reader.GetString(reader.GetOrdinal("route_linestring")),
            reader.GetDecimal(reader.GetOrdinal("current_load_weight")),
            reader.GetDecimal(reader.GetOrdinal("current_load_volume")),
            ReadNullableTimestamp(reader, "started_at"),
            ReadNullableTimestamp(reader, "finished_at"),
            reader.GetInt32(reader.GetOrdinal("version")),
            Enum.Parse<TripStatus>(reader.GetString(reader.GetOrdinal("status"))),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")),
            reader.IsDBNull(reader.GetOrdinal("trip_code")) ? null : reader.GetString(reader.GetOrdinal("trip_code")),
            ReadNullableTimestamp(reader, "scheduled_departure_at"));
    }

    private static DateTimeOffset? ReadNullableTimestamp(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private const string SelectTripSql = """
        SELECT
            id,
            trip_code,
            driver_id,
            vehicle_id,
            origin_hub_id,
            dest_hub_id,
            ST_AsText(route_linestring) AS route_linestring,
            current_load_weight,
            current_load_volume,
            scheduled_departure_at,
            started_at,
            finished_at,
            version,
            status,
            created_at,
            updated_at
        FROM transport.trips
        """;
}
