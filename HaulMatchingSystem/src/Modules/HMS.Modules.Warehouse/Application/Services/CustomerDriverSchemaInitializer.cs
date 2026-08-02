using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Warehouse.Application.Services;

/// <summary>
/// Creates tables needed by Customer and Driver modules:
/// - quotation, payment, shipment_cancellation tables
/// - extends shipment_proposals with approved_at etc.
/// - extends shipments with cancel/delivered columns
/// </summary>
public sealed class CustomerDriverSchemaInitializer
{
    private readonly string _connStr;
    private readonly ILogger<CustomerDriverSchemaInitializer> _logger;

    public CustomerDriverSchemaInitializer(IConfiguration configuration, ILogger<CustomerDriverSchemaInitializer> logger)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=hms_db;Username=postgres;Password=hms_password_123";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // 1. Extend shipments table with new columns
        const string extendShipments = """
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS cancel_reason text;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS cancelled_at timestamptz;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS cancelled_by uuid;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS delivered_at timestamptz;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS delivered_by uuid;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS delivery_note text;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS proof_image_url text;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS dest_address_name text;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS dest_latitude double precision;
            ALTER TABLE warehouse.shipments ADD COLUMN IF NOT EXISTS dest_longitude double precision;
        """;
        await ExecuteSql(conn, extendShipments, ct);

        // 2. Create quotations table (enriched per spec)
        const string createQuotations = """
            CREATE TABLE IF NOT EXISTS warehouse.quotations (
                id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                proposal_id         uuid NOT NULL,
                quotation_code      text NOT NULL,
                shipping_fee        numeric(12,2) NOT NULL DEFAULT 0,
                deposit_amount      numeric(12,2) NOT NULL DEFAULT 0,
                currency            text NOT NULL DEFAULT 'VND',
                status              text NOT NULL DEFAULT 'Draft',
                quoted_by           uuid,
                quoted_at           timestamptz,
                sent_at             timestamptz,
                expires_at          timestamptz,
                accepted_at         timestamptz,
                expired_at          timestamptz,
                cancelled_at        timestamptz,
                created_at          timestamptz NOT NULL DEFAULT now(),
                updated_at          timestamptz NOT NULL DEFAULT now(),
                is_deleted          boolean NOT NULL DEFAULT FALSE,
                row_version         bytea
            );
        """;
        await ExecuteSql(conn, createQuotations, ct);
        // Ensure indexes exist even if table was created in a prior run
        const string quotationsIndexes = """
            CREATE INDEX IF NOT EXISTS idx_quotations_proposal_id ON warehouse.quotations (proposal_id);
            CREATE INDEX IF NOT EXISTS idx_quotations_status ON warehouse.quotations (status);
        """;
        await ExecuteSql(conn, quotationsIndexes, ct);
        const string quotationsUniqueIndex = """
            CREATE UNIQUE INDEX IF NOT EXISTS uq_quotations_active_per_proposal
                ON warehouse.quotations (proposal_id)
                WHERE status IN ('Draft', 'Sent') AND is_deleted = FALSE;
        """;
        await ExecuteSql(conn, quotationsUniqueIndex, ct);

        // 3. Create payments table (enriched per spec)
        const string createPayments = """
            CREATE TABLE IF NOT EXISTS warehouse.payments (
                id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                quotation_id        uuid NOT NULL,
                shipment_id         uuid NOT NULL,
                customer_id         uuid NOT NULL,
                payment_type        text NOT NULL,
                amount              numeric(12,2) NOT NULL,
                currency            text NOT NULL DEFAULT 'VND',
                payment_method      text,
                status              text NOT NULL DEFAULT 'Pending',
                transaction_reference text,
                idempotency_key     text,
                paid_at             timestamptz,
                confirmed_at        timestamptz,
                confirmed_by        uuid,
                failure_reason      text,
                created_at          timestamptz NOT NULL DEFAULT now(),
                updated_at          timestamptz NOT NULL DEFAULT now(),
                is_deleted          boolean NOT NULL DEFAULT FALSE,
                row_version         bytea
            );
        """;
        await ExecuteSql(conn, createPayments, ct);
        // Ensure customer_id column exists (added after initial table creation)
        const string paymentsAlterAdd = """
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS customer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS quotation_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS shipment_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS payment_type text NOT NULL DEFAULT 'Deposit';
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS amount numeric(12,2) NOT NULL DEFAULT 0;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS currency text NOT NULL DEFAULT 'VND';
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS payment_method text;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'Pending';
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS transaction_reference text;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS idempotency_key text;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS paid_at timestamptz;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS confirmed_at timestamptz;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS confirmed_by uuid;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS failure_reason text;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE warehouse.payments ADD COLUMN IF NOT EXISTS row_version bytea;
        """;
        await ExecuteSql(conn, paymentsAlterAdd, ct);
        // Ensure indexes exist even if table was created in a prior run
        const string paymentsIndexes = """
            CREATE INDEX IF NOT EXISTS idx_payments_quotation_id ON warehouse.payments (quotation_id);
            CREATE INDEX IF NOT EXISTS idx_payments_shipment_id ON warehouse.payments (shipment_id);
            CREATE INDEX IF NOT EXISTS idx_payments_customer_id ON warehouse.payments (customer_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_payments_transaction_ref ON warehouse.payments (transaction_reference) WHERE transaction_reference IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_payments_idempotency_key ON warehouse.payments (idempotency_key) WHERE idempotency_key IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_payments_deposit_paid_per_quotation
                ON warehouse.payments (quotation_id)
                WHERE payment_type = 'Deposit' AND status = 'Paid' AND is_deleted = FALSE;
        """;
        await ExecuteSql(conn, paymentsIndexes, ct);

        // 4. Extend shipment_proposals with review fields
        const string extendProposals = """
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS reviewed_at timestamptz;
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS reviewed_by uuid;
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS approved_at timestamptz;
            ALTER TABLE warehouse.shipment_proposals ADD COLUMN IF NOT EXISTS approved_by uuid;
        """;
        await ExecuteSql(conn, extendProposals, ct);

        // 5. Create trip_incidents table for Driver incident reporting
        const string createIncidents = """
            CREATE TABLE IF NOT EXISTS transport.trip_incidents (
                id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                trip_id             uuid NOT NULL REFERENCES transport.trips(id) ON DELETE RESTRICT,
                shipment_id         uuid,
                reported_by         uuid NOT NULL,
                incident_type       text NOT NULL,
                description         text NOT NULL,
                occurred_at         timestamptz NOT NULL,
                created_at          timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS idx_trip_incidents_trip_id ON transport.trip_incidents (trip_id);
        """;
        await ExecuteSql(conn, createIncidents, ct);

        // 6. Extend trips with driver-trip management columns
        const string extendTrips = """
            ALTER TABLE transport.trips ADD COLUMN IF NOT EXISTS started_at_v2 timestamptz;
            ALTER TABLE transport.trips ADD COLUMN IF NOT EXISTS completed_at timestamptz;
            ALTER TABLE transport.trips ADD COLUMN IF NOT EXISTS trip_code text;
        """;
        await ExecuteSql(conn, extendTrips, ct);

        // 7. Create audit_log table
        const string createAuditLog = """
            CREATE TABLE IF NOT EXISTS shared.audit_log (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                entity_type     text NOT NULL,
                entity_id       uuid NOT NULL,
                action          text NOT NULL,
                performed_by    uuid,
                details         jsonb,
                created_at      timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS idx_audit_log_entity ON shared.audit_log (entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_audit_log_created_at ON shared.audit_log (created_at DESC);
        """;
        await ExecuteSql(conn, "CREATE SCHEMA IF NOT EXISTS shared;", ct);
        await ExecuteSql(conn, createAuditLog, ct);

        _logger.LogInformation("Customer/Driver schema initialized successfully.");
    }

    private static async Task ExecuteSql(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
