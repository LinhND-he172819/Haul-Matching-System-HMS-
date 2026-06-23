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
            DECLARE
                shipments_table regclass;
                trip_shipments_table regclass;
            BEGIN
                shipments_table := COALESCE(to_regclass('public.shipments'), to_regclass('warehouse.shipments'));

                IF shipments_table IS NOT NULL THEN
                    EXECUTE format(
                        'ALTER TABLE %s ADD COLUMN IF NOT EXISTS delivery_location geometry(Point, 4326)',
                        shipments_table);

                    EXECUTE format(
                        'CREATE INDEX IF NOT EXISTS ix_shipments_delivery_location_gist ON %s USING GIST (delivery_location) WHERE delivery_location IS NOT NULL',
                        shipments_table);

                    IF EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = shipments_table
                            AND attname = 'Status'
                            AND NOT attisdropped
                    ) THEN
                        EXECUTE format(
                            'CREATE INDEX IF NOT EXISTS ix_shipments_status ON %s ("Status")',
                            shipments_table);
                    ELSIF EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = shipments_table
                            AND attname = 'status'
                            AND NOT attisdropped
                    ) THEN
                        EXECUTE format(
                            'CREATE INDEX IF NOT EXISTS ix_shipments_status ON %s (status)',
                            shipments_table);
                    END IF;
                END IF;

                trip_shipments_table := to_regclass('public.trip_shipments');

                IF trip_shipments_table IS NOT NULL THEN
                    IF EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = trip_shipments_table
                            AND attname = 'ShipmentId'
                            AND NOT attisdropped
                    ) AND EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = trip_shipments_table
                            AND attname = 'Status'
                            AND NOT attisdropped
                    ) THEN
                        EXECUTE format(
                            'CREATE INDEX IF NOT EXISTS ix_trip_shipments_shipment_status ON %s ("ShipmentId", "Status")',
                            trip_shipments_table);
                    ELSIF EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = trip_shipments_table
                            AND attname = 'shipment_id'
                            AND NOT attisdropped
                    ) AND EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = trip_shipments_table
                            AND attname = 'status'
                            AND NOT attisdropped
                    ) THEN
                        EXECUTE format(
                            'CREATE INDEX IF NOT EXISTS ix_trip_shipments_shipment_status ON %s (shipment_id, status)',
                            trip_shipments_table);
                    END IF;
                END IF;
            END $$;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Matching spatial schema initialized for point-to-line queries.");
    }
}
