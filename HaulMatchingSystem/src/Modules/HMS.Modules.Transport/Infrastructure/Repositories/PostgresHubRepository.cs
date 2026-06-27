using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace HMS.Modules.Transport.Infrastructure.Repositories;

public sealed class PostgresHubRepository : IHubRepository
{
    private readonly string _connectionString;

    public PostgresHubRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task AddAsync(Hub hub, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var hubTableName = await ResolveHubTableNameAsync(connection, cancellationToken);

        command.CommandText = $"""
            INSERT INTO {hubTableName} (
                id,
                name,
                address,
                geo_location,
                created_at,
                updated_at,
                is_deleted
            )
            VALUES (
                @id,
                @name,
                @address,
                ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326)::geography,
                @created_at,
                @updated_at,
                FALSE
            );
            """;

        AddHubParameters(command, hub);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Hub>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var hubTableName = await ResolveHubTableNameAsync(connection, cancellationToken);

        var filters = new List<string> { "COALESCE(is_deleted, FALSE) = FALSE" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(name ILIKE @search OR address ILIKE @search)");
            command.Parameters.Add("search", NpgsqlDbType.Text).Value = $"%{search.Trim()}%";
        }

        command.CommandText = $"""
            {BuildSelectHubSql(hubTableName)}
            WHERE {string.Join(" AND ", filters)}
            ORDER BY name ASC;
            """;

        var hubs = new List<Hub>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hubs.Add(ReadHub(reader));
        }

        return hubs;
    }

    public async Task<Hub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var hubTableName = await ResolveHubTableNameAsync(connection, cancellationToken);

        command.CommandText = $"""
            {BuildSelectHubSql(hubTableName)}
            WHERE id = @id AND COALESCE(is_deleted, FALSE) = FALSE;
            """;
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadHub(reader) : null;
    }

    public async Task UpdateAsync(Hub hub, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var hubTableName = await ResolveHubTableNameAsync(connection, cancellationToken);

        command.CommandText = $"""
            UPDATE {hubTableName}
            SET
                name = @name,
                address = @address,
                geo_location = ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326)::geography,
                updated_at = @updated_at
            WHERE id = @id AND COALESCE(is_deleted, FALSE) = FALSE;
            """;

        AddHubParameters(command, hub);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private static async Task<string> ResolveHubTableNameAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(
                (
                    SELECT format('%I.%I', ns.nspname, cls.relname)
                    FROM pg_constraint con
                    JOIN pg_class vehicle_cls ON vehicle_cls.oid = con.conrelid
                    JOIN pg_namespace vehicle_ns ON vehicle_ns.oid = vehicle_cls.relnamespace
                    JOIN pg_class cls ON cls.oid = con.confrelid
                    JOIN pg_namespace ns ON ns.oid = cls.relnamespace
                    WHERE con.contype = 'f'
                        AND vehicle_ns.nspname = 'public'
                        AND vehicle_cls.relname = 'vehicles'
                        AND con.conname = 'vehicles_hub_id_fkey'
                    LIMIT 1
                ),
                CASE
                    WHEN to_regclass('public.hubs') IS NOT NULL THEN 'public.hubs'
                    ELSE 'identity.hubs'
                END
            );
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result?.ToString() ?? "identity.hubs";
    }

    private static void AddHubParameters(NpgsqlCommand command, Hub hub)
    {
        command.Parameters.AddWithValue("id", hub.Id);
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = hub.Name;
        command.Parameters.Add("address", NpgsqlDbType.Text).Value = hub.Address;
        command.Parameters.AddWithValue("latitude", hub.Latitude);
        command.Parameters.AddWithValue("longitude", hub.Longitude);
        command.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = hub.CreatedAt.ToUniversalTime();
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = hub.UpdatedAt.ToUniversalTime();
    }

    private static Hub ReadHub(NpgsqlDataReader reader)
    {
        return Hub.Rehydrate(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.GetString(reader.GetOrdinal("address")),
            reader.GetDouble(reader.GetOrdinal("latitude")),
            reader.GetDouble(reader.GetOrdinal("longitude")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")));
    }

    private static string BuildSelectHubSql(string hubTableName)
    {
        return $"""
        SELECT
            id,
            name,
            address,
            ST_Y(geo_location::geometry) AS latitude,
            ST_X(geo_location::geometry) AS longitude,
            created_at,
            updated_at
        FROM {hubTableName}
        """;
    }
}
