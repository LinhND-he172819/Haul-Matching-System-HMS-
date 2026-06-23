using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Transport.Infrastructure.Schema;

public sealed class PostgresTransportSchemaInitializer : ITransportSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresTransportSchemaInitializer> _logger;

    public PostgresTransportSchemaInitializer(
        IConfiguration configuration,
        ILogger<PostgresTransportSchemaInitializer> logger)
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
            CREATE SCHEMA IF NOT EXISTS identity;
            CREATE SCHEMA IF NOT EXISTS transport;

            CREATE TABLE IF NOT EXISTS identity.hubs (
                id uuid PRIMARY KEY,
                name text NOT NULL,
                address text NOT NULL,
                geo_location geography(Point, 4326) NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                is_deleted boolean NOT NULL DEFAULT FALSE
            );

            ALTER TABLE identity.hubs
                ADD COLUMN IF NOT EXISTS name text,
                ADD COLUMN IF NOT EXISTS address text,
                ADD COLUMN IF NOT EXISTS geo_location geography(Point, 4326),
                ADD COLUMN IF NOT EXISTS created_at timestamptz NOT NULL DEFAULT now(),
                ADD COLUMN IF NOT EXISTS updated_at timestamptz NOT NULL DEFAULT now(),
                ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;

            CREATE INDEX IF NOT EXISTS ix_identity_hubs_name
                ON identity.hubs (name)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_identity_hubs_geo_location_gist
                ON identity.hubs
                USING GIST (geo_location)
                WHERE is_deleted = FALSE;

            CREATE TABLE IF NOT EXISTS transport.trips (
                id uuid PRIMARY KEY,
                driver_id uuid NOT NULL,
                vehicle_id uuid NOT NULL,
                origin_hub_id uuid NOT NULL,
                dest_hub_id uuid NOT NULL,
                route_linestring geometry(LineString, 4326) NOT NULL,
                current_load_weight numeric(12, 2) NOT NULL DEFAULT 0,
                current_load_volume numeric(12, 2) NOT NULL DEFAULT 0,
                started_at timestamptz NULL,
                finished_at timestamptz NULL,
                version integer NOT NULL DEFAULT 1,
                status text NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                is_deleted boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT ck_transport_trips_current_load_weight_nonnegative CHECK (current_load_weight >= 0),
                CONSTRAINT ck_transport_trips_current_load_volume_nonnegative CHECK (current_load_volume >= 0),
                CONSTRAINT ck_transport_trips_status CHECK (status IN ('Active', 'Completed', 'Breakdown'))
            );

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'transport'
                        AND table_name = 'trips'
                        AND column_name = 'route_linestring'
                        AND udt_name <> 'geometry'
                ) THEN
                    ALTER TABLE transport.trips
                    ALTER COLUMN route_linestring TYPE geometry(LineString, 4326)
                    USING ST_SetSRID(ST_GeomFromText(route_linestring), 4326);
                ELSE
                    ALTER TABLE transport.trips
                    ALTER COLUMN route_linestring TYPE geometry(LineString, 4326)
                    USING ST_SetSRID(route_linestring, 4326)::geometry(LineString, 4326);
                END IF;
            END $$;

            ALTER TABLE transport.trips
            ALTER COLUMN route_linestring SET NOT NULL;

            CREATE INDEX IF NOT EXISTS ix_transport_trips_driver_id
                ON transport.trips (driver_id);

            CREATE INDEX IF NOT EXISTS ix_transport_trips_status
                ON transport.trips (status);

            CREATE INDEX IF NOT EXISTS ix_transport_trips_route_linestring_gist
                ON transport.trips
                USING GIST (route_linestring)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_trips_route_linestring_geography_gist
                ON transport.trips
                USING GIST ((route_linestring::geography))
                WHERE is_deleted = FALSE;

            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Transport schema initialized with PostGIS LineString storage and spatial indexes.");
    }
}
