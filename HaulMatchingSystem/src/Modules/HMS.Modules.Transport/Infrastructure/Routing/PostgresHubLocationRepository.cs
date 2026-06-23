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
        command.CommandText = """
            SELECT
                ST_X(geo_location::geometry) AS longitude,
                ST_Y(geo_location::geometry) AS latitude
            FROM identity.hubs
            WHERE id = @hub_id
                AND is_deleted = FALSE;
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
}
