using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HMS.Modules.Matching.Infrastructure
{
    /// <summary>
    /// Repository for Quotation and Payment operations using raw SQL.
    /// Uses NpgsqlConnection directly for transaction support.
    /// </summary>
    public class QuotationPaymentRepository : IQuotationPaymentRepository
    {
        private readonly MatchingDbContext _db;
        private readonly string _connStr;

        public QuotationPaymentRepository(MatchingDbContext db, IConfiguration configuration)
        {
            _db = db;
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
        }

        // ── Quotation ──

        public async Task<Quotation?> GetQuotationByIdAsync(Guid quotationId, CancellationToken ct)
        {
            return await _db.Quotations.FindAsync(new object[] { quotationId }, ct);
        }

        public async Task<List<Quotation>> GetQuotationsByProposalIdAsync(Guid proposalId, CancellationToken ct)
        {
            return await _db.Quotations
                .Where(q => q.ProposalId == proposalId && !q.IsDeleted)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<Quotation?> GetActiveDraftOrSentQuotationAsync(Guid proposalId, CancellationToken ct)
        {
            return await _db.Quotations.FirstOrDefaultAsync(
                q => q.ProposalId == proposalId
                    && (q.Status == "Draft" || q.Status == "Sent")
                    && !q.IsDeleted,
                ct);
        }

        public async Task AddQuotationAsync(Quotation quotation, CancellationToken ct)
        {
            await _db.Quotations.AddAsync(quotation, ct);
        }

        public async Task UpdateQuotationAsync(Quotation quotation, CancellationToken ct)
        {
            _db.Quotations.Update(quotation);
        }

        // ── Payment ──

        public async Task<Payment?> GetPaymentByIdAsync(Guid paymentId, CancellationToken ct)
        {
            return await _db.Payments.FindAsync(new object[] { paymentId }, ct);
        }

        public async Task<Payment?> GetPaymentByTransactionRefAsync(string transactionReference, CancellationToken ct)
        {
            return await _db.Payments.FirstOrDefaultAsync(
                p => p.TransactionReference == transactionReference && !p.IsDeleted,
                ct);
        }

        public async Task<Payment?> GetPaymentByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        {
            return await _db.Payments.FirstOrDefaultAsync(
                p => p.IdempotencyKey == idempotencyKey && !p.IsDeleted,
                ct);
        }

        public async Task<Payment?> GetSuccessfulDepositForQuotationAsync(Guid quotationId, CancellationToken ct)
        {
            return await _db.Payments.FirstOrDefaultAsync(
                p => p.QuotationId == quotationId
                    && p.PaymentType == "Deposit"
                    && p.Status == "Paid"
                    && !p.IsDeleted,
                ct);
        }

        public async Task<List<Payment>> GetPaymentsByQuotationIdAsync(Guid quotationId, CancellationToken ct)
        {
            return await _db.Payments
                .Where(p => p.QuotationId == quotationId && !p.IsDeleted)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<Payment>> GetPaymentsByShipmentIdAsync(Guid shipmentId, CancellationToken ct)
        {
            return await _db.Payments
                .Where(p => p.ShipmentId == shipmentId && !p.IsDeleted)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddPaymentAsync(Payment payment, CancellationToken ct)
        {
            await _db.Payments.AddAsync(payment, ct);
        }

        public async Task UpdatePaymentAsync(Payment payment, CancellationToken ct)
        {
            _db.Payments.Update(payment);
        }

        // ── Combined ──

        public async Task<Quotation?> GetQuotationWithProposalAsync(Guid quotationId, CancellationToken ct)
        {
            return await _db.Quotations.FirstOrDefaultAsync(
                q => q.Id == quotationId && !q.IsDeleted, ct);
        }

        public async Task<ShipmentProposal?> GetProposalByIdAsync(Guid proposalId, CancellationToken ct)
        {
            return await _db.ShipmentProposals.FindAsync(new object[] { proposalId }, ct);
        }

        public async Task<Shipment?> GetShipmentByIdAsync(Guid shipmentId, CancellationToken ct)
        {
            return await _db.Shipments.FindAsync(new object[] { shipmentId }, ct);
        }

        public async Task<Vehicle?> GetVehicleByIdAsync(Guid vehicleId, CancellationToken ct)
        {
            return await _db.Vehicles.FindAsync(new object[] { vehicleId }, ct);
        }

        public async Task<Trip?> GetTripByIdAsync(Guid tripId, CancellationToken ct)
        {
            return await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, ct);
        }

        public async Task<Guid?> GetTripPostIdForProposalAsync(Guid proposalId, CancellationToken ct)
        {
            var proposal = await _db.ShipmentProposals.FindAsync(new object[] { proposalId }, ct);
            return proposal?.TripPostId;
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct)
        {
            return await _db.SaveChangesAsync(ct);
        }

        // ── Capacity ──

        public async Task<bool> HasSufficientCapacityAsync(Guid tripId, decimal weightKg, decimal volumeCbm, CancellationToken ct)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, ct);
            if (trip == null) return false;

            // Get vehicle capacity
            var vehicle = await _db.Vehicles.FindAsync(new object[] { trip.VehicleId }, ct);
            if (vehicle == null) return false;

            var remainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight;
            var remainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume;

            return remainingWeight >= weightKg && remainingVolume >= volumeCbm;
        }

        public async Task UpdateTripCapacityAsync(Guid tripId, decimal weightDelta, decimal volumeDelta, CancellationToken ct)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted, ct);
            if (trip == null)
                throw new InvalidOperationException($"Trip {tripId} not found.");

            trip.CurrentLoadWeight += weightDelta;
            trip.CurrentLoadVolume += volumeDelta;
            trip.Version++;

            _db.Trips.Update(trip);
        }

        public async Task LinkShipmentToTripAsync(Guid tripId, Guid shipmentId, CancellationToken ct)
        {
            var existing = await _db.TripShipments.FirstOrDefaultAsync(
                ts => ts.TripId == tripId && ts.ShipmentId == shipmentId, ct);

            if (existing != null) return; // Already linked

            var tripShipment = new TripShipment
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                ShipmentId = shipmentId,
                Status = "Matched",
                AcceptedAt = DateTime.UtcNow
            };

            await _db.TripShipments.AddAsync(tripShipment, ct);
        }

        // ── Audit ──

        public async Task InsertAuditLogAsync(string entityType, Guid entityId, string action, string? details, Guid? performedBy, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO shared.audit_log (entity_type, entity_id, action, performed_by, details, created_at)
                VALUES (@entity_type, @entity_id, @action, @performed_by, @details::jsonb, NOW());
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("entity_type", entityType);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("performed_by", (object)performedBy! ?? DBNull.Value);
            cmd.Parameters.AddWithValue("details", (object)(details ?? "{}")!);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Notifications ──

        public async Task SaveNotificationAsync(Guid userId, string title, string message, string? entityType, Guid? entityId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            // Create notifications table if not exists
            const string createTable = """
                CREATE SCHEMA IF NOT EXISTS shared;
                CREATE TABLE IF NOT EXISTS shared.notifications (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id uuid NOT NULL,
                    title text NOT NULL,
                    message text NOT NULL,
                    entity_type text,
                    entity_id uuid,
                    is_read boolean NOT NULL DEFAULT FALSE,
                    created_at timestamptz NOT NULL DEFAULT NOW()
                );
            """;
            await using (var createCmd = new NpgsqlCommand(createTable, conn))
            {
                await createCmd.ExecuteNonQueryAsync(ct);
            }

            const string sql = """
                INSERT INTO shared.notifications (user_id, title, message, entity_type, entity_id, created_at)
                VALUES (@user_id, @title, @message, @entity_type, @entity_id, NOW());
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("message", message);
            cmd.Parameters.AddWithValue("entity_type", (object)entityType! ?? DBNull.Value);
            cmd.Parameters.AddWithValue("entity_id", (object)entityId! ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
