using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Application.Services;

/// <summary>
/// Creates the warehouse schema and shipment_status_history table if they don't exist.
/// Called at application startup.
/// </summary>
public sealed class PostgresWarehouseSchemaInitializer
{
    private readonly string _connStr;
    private readonly ILogger<PostgresWarehouseSchemaInitializer> _logger;

    public PostgresWarehouseSchemaInitializer(IConfiguration configuration, ILogger<PostgresWarehouseSchemaInitializer> logger)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=hms_db;Username=postgres;Password=hms_password_123";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // ── Create warehouse schema ──
        await using (var cmd = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS warehouse;", conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Create shipments table (if not exists) ──
        const string createShipmentsTable = """
            CREATE TABLE IF NOT EXISTS warehouse.shipments (
                id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                qr_code                 text NOT NULL,
                customer_id             uuid,
                receiver_name           text,
                receiver_phone          text,
                dest_address            text,
                dest_location           geography(Point, 4326),
                cargo_type              text,
                weight_kg               numeric(12,2) NOT NULL DEFAULT 0,
                volume_cbm              numeric(12,2) NOT NULL DEFAULT 0,
                special_handling_note   text,
                cod_amount              numeric(12,2),
                shipping_fee            numeric(12,2),
                status                  text NOT NULL DEFAULT 'Draft',
                current_hub_id          uuid,
                intake_confirmed_by     uuid,
                intake_confirmed_at     timestamptz,
                created_at              timestamptz NOT NULL DEFAULT now(),
                updated_at              timestamptz NOT NULL DEFAULT now(),
                is_deleted              boolean NOT NULL DEFAULT FALSE
            );
        """;
        await using (var cmd = new NpgsqlCommand(createShipmentsTable, conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Create shipment_status_history table (append-only audit) ──
        const string createHistoryTable = """
            CREATE TABLE IF NOT EXISTS warehouse.shipment_status_history (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                shipment_id     uuid NOT NULL REFERENCES warehouse.shipments(id),
                from_status     text NOT NULL,
                to_status       text NOT NULL,
                performed_by    uuid,
                reason          text,
                occurred_at     timestamptz NOT NULL DEFAULT now()
            );

            -- Fast lookup by shipment (most common query pattern)
            CREATE INDEX IF NOT EXISTS ix_shipment_status_history_shipment_id
                ON warehouse.shipment_status_history (shipment_id, occurred_at DESC);

            -- Prevent updates and deletes (append-only audit)
            -- PostgreSQL doesn't have a direct "readonly" for rows, but we create a
            -- RULE or trigger to reject modifications. Using a simple trigger:
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_trigger WHERE tgname = 'trg_prevent_shipment_history_modification'
                ) THEN
                    CREATE OR REPLACE FUNCTION warehouse.prevent_shipment_history_modification()
                    RETURNS TRIGGER AS $func$
                    BEGIN
                        RAISE EXCEPTION 'Shipment status history is append-only. Updates and deletes are not allowed.';
                        RETURN NULL;
                    END;
                    $func$ LANGUAGE plpgsql;

                    CREATE TRIGGER trg_prevent_shipment_history_modification
                        BEFORE UPDATE OR DELETE ON warehouse.shipment_status_history
                        FOR EACH ROW
                        EXECUTE FUNCTION warehouse.prevent_shipment_history_modification();
                END IF;
            END $$;
        """;
        await using (var cmd = new NpgsqlCommand(createHistoryTable, conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.LogInformation("Warehouse schema and shipment_status_history initialized.");
    }
}
