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
            -- ── Driver External Shipment Declaration columns ──
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS proposal_source text NOT NULL DEFAULT 'Customer';
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS driver_id uuid;
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS requested_trip_id uuid;

            -- Index for driver proposal queries
            CREATE INDEX IF NOT EXISTS ix_shipment_proposals_driver_id
                ON warehouse.shipment_proposals (driver_id) WHERE driver_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_shipment_proposals_proposal_source
                ON warehouse.shipment_proposals (proposal_source);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        // Add CHECK constraints separately (DO block needs semicolons inside)
        await using var constraintCmd = connection.CreateCommand();
        constraintCmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_proposal_source_customer') THEN
                    ALTER TABLE warehouse.shipment_proposals
                    ADD CONSTRAINT chk_proposal_source_customer
                    CHECK (proposal_source <> 'Customer' OR (trip_post_id IS NOT NULL AND customer_id IS NOT NULL AND requested_trip_id IS NULL));
                END IF;
            END $$;

            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_proposal_source_driver') THEN
                    ALTER TABLE warehouse.shipment_proposals
                    ADD CONSTRAINT chk_proposal_source_driver
                    CHECK (proposal_source <> 'Driver' OR (requested_trip_id IS NOT NULL AND driver_id IS NOT NULL AND trip_post_id IS NULL AND customer_id IS NULL));
                END IF;
            END $$;
            """;
        await constraintCmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Matching spatial indexes initialized on warehouse and transport schemas.");
    }
}
