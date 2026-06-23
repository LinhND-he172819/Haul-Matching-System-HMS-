using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Transport.Infrastructure.Routing;

public sealed class PostgresHubLocationRepository : IHubLocationRepository
{
    private readonly string _connectionString;

    public PostgresHubLocationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task<HubCoordinate?> GetCoordinateAsync(
        Guid hubId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var hubTableName = await ResolveHubTableNameAsync(connection, cancellationToken);

        command.CommandText = $"""
            SELECT
                ST_X(geo_location::geometry) AS longitude,
                ST_Y(geo_location::geometry) AS latitude
            FROM {hubTableName}
            WHERE id = @hub_id
                AND COALESCE(is_deleted, FALSE) = FALSE;
            """;
        command.Parameters.AddWithValue("hub_id", hubId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new HubCoordinate(
            hubId,
            reader.GetDouble(reader.GetOrdinal("longitude")),
            reader.GetDouble(reader.GetOrdinal("latitude")));
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
}
