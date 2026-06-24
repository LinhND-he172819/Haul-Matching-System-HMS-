using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Transport.Infrastructure.Repositories;

public sealed class PostgresVehicleRepository : IVehicleRepository
{
    private readonly string _connectionString;

    public PostgresVehicleRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO vehicles (
                "Id",
                code,
                license_plate,
                hub_id,
                vehicle_type,
                "MaxWeightKg",
                "MaxVolumeCbm",
                status,
                created_at,
                updated_at,
                is_deleted
            )
            VALUES (
                @id,
                @code,
                @license_plate,
                @hub_id,
                @vehicle_type,
                @max_weight_kg,
                @max_volume_cbm,
                @status,
                @created_at,
                @updated_at,
                FALSE
            );
            """;

        AddVehicleParameters(command, vehicle);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Vehicle>> ListAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var filters = new List<string> { "COALESCE(is_deleted, FALSE) = FALSE" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(code ILIKE @search OR license_plate ILIKE @search OR vehicle_type ILIKE @search)");
            command.Parameters.Add("search", NpgsqlDbType.Text).Value = $"%{search.Trim()}%";
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add("status = @status");
            command.Parameters.Add("status", NpgsqlDbType.Text).Value = status.Trim();
        }

        command.CommandText = $"""
            {SelectVehicleSql}
            WHERE {string.Join(" AND ", filters)}
            ORDER BY code ASC;
            """;

        var vehicles = new List<Vehicle>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            vehicles.Add(ReadVehicle(reader));
        }

        return vehicles;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
            {SelectVehicleSql}
            WHERE "Id" = @id AND COALESCE(is_deleted, FALSE) = FALSE;
            """;
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadVehicle(reader) : null;
    }

    public async Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE vehicles
            SET
                code = @code,
                license_plate = @license_plate,
                hub_id = @hub_id,
                vehicle_type = @vehicle_type,
                "MaxWeightKg" = @max_weight_kg,
                "MaxVolumeCbm" = @max_volume_cbm,
                status = @status,
                updated_at = @updated_at
            WHERE "Id" = @id AND COALESCE(is_deleted, FALSE) = FALSE;
            """;

        AddVehicleParameters(command, vehicle);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private static void AddVehicleParameters(NpgsqlCommand command, Vehicle vehicle)
    {
        command.Parameters.AddWithValue("id", vehicle.Id);
        command.Parameters.Add("code", NpgsqlDbType.Text).Value = vehicle.Code;
        command.Parameters.Add("license_plate", NpgsqlDbType.Text).Value = vehicle.LicensePlate;
        command.Parameters.AddWithValue("hub_id", vehicle.HubId);
        command.Parameters.Add("vehicle_type", NpgsqlDbType.Text).Value = vehicle.VehicleType;
        command.Parameters.Add("max_weight_kg", NpgsqlDbType.Numeric).Value = vehicle.MaxWeightKg;
        command.Parameters.Add("max_volume_cbm", NpgsqlDbType.Numeric).Value = vehicle.MaxVolumeCbm;
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = vehicle.Status;
        command.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = vehicle.CreatedAt.ToUniversalTime();
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = vehicle.UpdatedAt.ToUniversalTime();
    }

    private static Vehicle ReadVehicle(NpgsqlDataReader reader)
    {
        return Vehicle.Rehydrate(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("code")),
            reader.GetString(reader.GetOrdinal("license_plate")),
            reader.GetGuid(reader.GetOrdinal("hub_id")),
            reader.GetString(reader.GetOrdinal("vehicle_type")),
            reader.GetDecimal(reader.GetOrdinal("max_weight_kg")),
            reader.GetDecimal(reader.GetOrdinal("max_volume_cbm")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")));
    }

    private const string SelectVehicleSql = """
        SELECT
            "Id" AS id,
            code,
            license_plate,
            hub_id,
            vehicle_type,
            "MaxWeightKg" AS max_weight_kg,
            "MaxVolumeCbm" AS max_volume_cbm,
            status,
            created_at,
            updated_at
        FROM vehicles
        """;
}
