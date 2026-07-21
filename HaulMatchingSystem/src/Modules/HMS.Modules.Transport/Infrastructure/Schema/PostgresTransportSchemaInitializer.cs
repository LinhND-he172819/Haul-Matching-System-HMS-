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
            CREATE SCHEMA IF NOT EXISTS transport;

            CREATE TABLE IF NOT EXISTS transport.vehicles (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                code text NOT NULL,
                license_plate text NOT NULL,
                hub_id uuid NOT NULL REFERENCES identity.hubs(id),
                vehicle_type text NOT NULL,
                max_weight_kg numeric(12, 2) NOT NULL,
                max_volume_cbm numeric(12, 2) NOT NULL,
                status text NOT NULL DEFAULT 'Available',
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                is_deleted boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT ck_transport_vehicles_weight_positive CHECK (max_weight_kg > 0),
                CONSTRAINT ck_transport_vehicles_volume_positive CHECK (max_volume_cbm > 0)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_transport_vehicles_code
                ON transport.vehicles (code)
                WHERE is_deleted = FALSE;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_transport_vehicles_license_plate
                ON transport.vehicles (license_plate)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_vehicles_hub_id
                ON transport.vehicles (hub_id)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_vehicles_status
                ON transport.vehicles (status)
                WHERE is_deleted = FALSE;

            DO $$
            BEGIN
                IF to_regclass('identity.vehicles') IS NOT NULL THEN
                    INSERT INTO transport.vehicles (
                        id,
                        code,
                        license_plate,
                        hub_id,
                        vehicle_type,
                        max_weight_kg,
                        max_volume_cbm,
                        status,
                        created_at,
                        updated_at,
                        is_deleted
                    )
                    SELECT
                        id,
                        'LEGACY-' || left(id::text, 8),
                        license_plate,
                        hub_id,
                        truck_type,
                        max_weight_kg,
                        max_volume_cbm,
                        'Available',
                        created_at,
                        updated_at,
                        is_deleted
                    FROM identity.vehicles
                    ON CONFLICT (id) DO NOTHING;
                END IF;
            END $$;

            CREATE TABLE IF NOT EXISTS transport.trips (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                driver_id uuid NOT NULL REFERENCES identity.users(id),
                vehicle_id uuid NOT NULL REFERENCES transport.vehicles(id),
                origin_hub_id uuid NOT NULL REFERENCES identity.hubs(id),
                dest_hub_id uuid NOT NULL REFERENCES identity.hubs(id),
                route_linestring geometry(LineString, 4326) NOT NULL,
                current_load_weight numeric(12, 2) NOT NULL DEFAULT 0,
                current_load_volume numeric(12, 2) NOT NULL DEFAULT 0,
                started_at timestamptz NULL,
                finished_at timestamptz NULL,
                version integer NOT NULL DEFAULT 1,
                status text NOT NULL DEFAULT 'Active',
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                is_deleted boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT ck_transport_trips_weight_nonnegative CHECK (current_load_weight >= 0),
                CONSTRAINT ck_transport_trips_volume_nonnegative CHECK (current_load_volume >= 0),
                CONSTRAINT ck_transport_trips_status CHECK (status IN ('Active', 'Completed', 'Breakdown'))
            );

            CREATE INDEX IF NOT EXISTS ix_transport_trips_driver_id
                ON transport.trips (driver_id);

            CREATE INDEX IF NOT EXISTS ix_transport_trips_vehicle_id
                ON transport.trips (vehicle_id);

            CREATE INDEX IF NOT EXISTS ix_transport_trips_status
                ON transport.trips (status)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_trips_route_gist
                ON transport.trips USING GIST (route_linestring)
                WHERE is_deleted = FALSE;

            DO $$
            DECLARE
                constraint_record record;
            BEGIN
                FOR constraint_record IN
                    SELECT constraint_name
                    FROM information_schema.table_constraints
                    WHERE table_schema = 'transport'
                        AND table_name = 'trips'
                        AND constraint_type = 'FOREIGN KEY'
                        AND constraint_name IN (
                            SELECT tc.constraint_name
                            FROM information_schema.table_constraints tc
                            JOIN information_schema.key_column_usage kcu
                                ON kcu.constraint_schema = tc.constraint_schema
                                AND kcu.constraint_name = tc.constraint_name
                            WHERE tc.table_schema = 'transport'
                                AND tc.table_name = 'trips'
                                AND kcu.column_name = 'vehicle_id'
                        )
                LOOP
                    EXECUTE format(
                        'ALTER TABLE transport.trips DROP CONSTRAINT %I',
                        constraint_record.constraint_name);
                END LOOP;

                ALTER TABLE transport.trips
                    ADD CONSTRAINT fk_transport_trips_vehicle
                    FOREIGN KEY (vehicle_id) REFERENCES transport.vehicles(id);
            END $$;

            CREATE TABLE IF NOT EXISTS transport.trip_shipments (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                trip_id uuid NOT NULL REFERENCES transport.trips(id),
                shipment_id uuid NOT NULL REFERENCES warehouse.shipments(id),
                transferred_from_trip_id uuid NULL REFERENCES transport.trips(id),
                delivery_sequence integer NOT NULL,
                status text NOT NULL,
                suggested_at timestamptz NOT NULL DEFAULT now(),
                responded_at timestamptz NULL,
                responded_by uuid NULL REFERENCES identity.users(id),
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_transport_active_trip_shipment
                ON transport.trip_shipments (shipment_id)
                WHERE status IN ('Suggested', 'Matched', 'In_Transit');

            CREATE INDEX IF NOT EXISTS ix_transport_trip_shipments_trip_status
                ON transport.trip_shipments (trip_id, status);

            CREATE TABLE IF NOT EXISTS transport.gps_logs (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                trip_id uuid NOT NULL REFERENCES transport.trips(id),
                lat numeric(9, 6) NOT NULL,
                lng numeric(9, 6) NOT NULL,
                speed numeric(7, 2) NULL,
                device_timestamp timestamptz NOT NULL,
                server_received_at timestamptz NOT NULL DEFAULT now(),
                idempotency_key text UNIQUE
            );

            CREATE INDEX IF NOT EXISTS ix_transport_gps_logs_trip_time
                ON transport.gps_logs (trip_id, device_timestamp DESC);

            CREATE TABLE IF NOT EXISTS transport.trip_exceptions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                trip_id uuid NOT NULL REFERENCES transport.trips(id),
                shipment_id uuid NULL REFERENCES warehouse.shipments(id),
                exception_type text NOT NULL,
                reason text NOT NULL,
                evidence_image_url text NULL,
                lat numeric(9, 6) NULL,
                lng numeric(9, 6) NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            -- Trip Posts (Đăng bài chuyến xe còn chỗ)
            CREATE TABLE IF NOT EXISTS transport.trip_posts (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                trip_id uuid NOT NULL REFERENCES transport.trips(id),
                created_by uuid NOT NULL REFERENCES identity.users(id),
                title varchar(200) NOT NULL,
                description text NULL,
                accept_until timestamptz NOT NULL,
                status varchar(30) NOT NULL DEFAULT 'Open',
                published_at timestamptz NULL,
                closed_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                is_deleted boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT ck_transport_trip_posts_status CHECK (status IN ('Open', 'Closed', 'Expired', 'Cancelled'))
            );

            CREATE INDEX IF NOT EXISTS ix_transport_trip_posts_trip_id
                ON transport.trip_posts (trip_id)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_trip_posts_status
                ON transport.trip_posts (status)
                WHERE is_deleted = FALSE;

            CREATE INDEX IF NOT EXISTS ix_transport_trip_posts_created_at
                ON transport.trip_posts (created_at DESC)
                WHERE is_deleted = FALSE;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_transport_trip_posts_open_per_trip
                ON transport.trip_posts (trip_id)
                WHERE status = 'Open' AND is_deleted = FALSE;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Transport schema initialized in schema transport.");
    }
}
