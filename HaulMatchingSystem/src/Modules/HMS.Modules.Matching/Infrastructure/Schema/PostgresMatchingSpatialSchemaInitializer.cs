using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Matching.Infrastructure.Schema;

public sealed class PostgresMatchingSpatialSchemaInitializer : IMatchingSpatialSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresMatchingSpatialSchemaInitializer> _logger;

    public PostgresMatchingSpatialSchemaInitializer(
        IConfiguration configuration,
        ILogger<PostgresMatchingSpatialSchemaInitializer> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE EXTENSION IF NOT EXISTS postgis;

            CREATE INDEX IF NOT EXISTS ix_warehouse_shipments_destination_gist
                ON warehouse.shipments USING GIST (dest_location)
                WHERE dest_location IS NOT NULL;

            CREATE INDEX IF NOT EXISTS ix_warehouse_shipments_status
                ON warehouse.shipments (status);

            CREATE INDEX IF NOT EXISTS ix_transport_trip_shipments_shipment_status
                ON transport.trip_shipments (shipment_id, status);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Matching spatial indexes initialized on warehouse and transport schemas.");
    }
}
