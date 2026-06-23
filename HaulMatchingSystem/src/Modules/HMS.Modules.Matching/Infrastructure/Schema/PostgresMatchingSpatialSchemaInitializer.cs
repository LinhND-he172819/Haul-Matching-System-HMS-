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

            DO $$
            BEGIN
                IF to_regclass('shipments') IS NOT NULL THEN
                    ALTER TABLE shipments
                    ADD COLUMN IF NOT EXISTS delivery_location geometry(Point, 4326);

                    CREATE INDEX IF NOT EXISTS ix_shipments_delivery_location_gist
                        ON shipments
                        USING GIST (delivery_location)
                        WHERE delivery_location IS NOT NULL;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'shipments'
                            AND column_name = 'Status'
                    ) THEN
                        CREATE INDEX IF NOT EXISTS ix_shipments_status
                            ON shipments ("Status");
                    ELSIF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'shipments'
                            AND column_name = 'status'
                    ) THEN
                        CREATE INDEX IF NOT EXISTS ix_shipments_status
                            ON shipments (status);
                    END IF;
                END IF;

                IF to_regclass('trip_shipments') IS NOT NULL THEN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'trip_shipments'
                            AND column_name = 'ShipmentId'
                    ) AND EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'trip_shipments'
                            AND column_name = 'Status'
                    ) THEN
                        CREATE INDEX IF NOT EXISTS ix_trip_shipments_shipment_status
                            ON trip_shipments ("ShipmentId", "Status");
                    ELSIF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'trip_shipments'
                            AND column_name = 'shipment_id'
                    ) AND EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'trip_shipments'
                            AND column_name = 'status'
                    ) THEN
                        CREATE INDEX IF NOT EXISTS ix_trip_shipments_shipment_status
                            ON trip_shipments (shipment_id, status);
                    END IF;
                END IF;
            END $$;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Matching spatial schema initialized for point-to-line queries.");
    }
}
