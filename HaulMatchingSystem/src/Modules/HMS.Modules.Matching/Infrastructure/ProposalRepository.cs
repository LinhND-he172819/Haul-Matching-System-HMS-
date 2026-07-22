using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace HMS.Modules.Matching.Infrastructure
{
    /// <summary>
    /// Repository for ShipmentProposal operations.
    /// Uses the same MatchingDbContext that connects to the PostgreSQL database.
    /// </summary>
    public class ProposalRepository : IProposalRepository
    {
        private readonly MatchingDbContext _db;
        private IDbContextTransaction? _tx;

        public ProposalRepository(MatchingDbContext db)
        {
            _db = db;
        }

        // â”€â”€ Proposal Queries â”€â”€

        public async Task<ShipmentProposal?> GetByIdAsync(Guid proposalId, CancellationToken ct)
        {
            return await _db.ShipmentProposals.FindAsync(new object[] { proposalId }, ct);
        }

        public async Task<ShipmentProposal?> GetPendingByShipmentAndTripPostAsync(
            Guid shipmentId, Guid tripPostId, CancellationToken ct)
        {
            return await _db.ShipmentProposals.FirstOrDefaultAsync(
                p => p.ShipmentId == shipmentId
                    && p.TripPostId == tripPostId
                    && p.Status == ProposalStatusConstants.Pending,
                ct);
        }

        public async Task<List<ShipmentProposal>> GetPendingByTripPostAsync(Guid tripPostId, CancellationToken ct)
        {
            return await _db.ShipmentProposals
                .Where(p => p.TripPostId == tripPostId && p.Status == ProposalStatusConstants.Pending)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<ShipmentProposal>> GetPendingByDriverAsync(Guid driverId, CancellationToken ct)
        {
            // Get all trip posts for trips driven by this driver, then get pending proposals
            var activeTrips = await _db.Trips
                .Where(t => t.DriverId == driverId && t.Status == "Active" && !t.IsDeleted)
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (!activeTrips.Any())
                return new List<ShipmentProposal>();

            // Get trip posts for these trips (query transport.trip_posts via raw SQL since we don't have a TripPost entity in MatchingDbContext)
            var tripPostIds = new List<Guid>();
            var connection = _db.Database.GetDbConnection();
            var shouldOpen = connection.State != System.Data.ConnectionState.Open;
            if (shouldOpen) await connection.OpenAsync();

            try
            {
                foreach (var tripId in activeTrips)
                {
                    using var cmd = connection.CreateCommand();
                    if (_tx is not null)
                    {
                        cmd.Transaction = _tx.GetDbTransaction();
                    }
                    cmd.CommandText = """
                        SELECT id FROM transport.trip_posts
                        WHERE trip_id = @trip_id AND status = 'Open' AND is_deleted = FALSE;
                    """;
                    var p = cmd.CreateParameter();
                    p.ParameterName = "trip_id";
                    p.Value = tripId;
                    cmd.Parameters.Add(p);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        tripPostIds.Add(reader.GetGuid(0));
                    }
                }
            }
            finally
            {
                if (shouldOpen) await connection.CloseAsync();
            }

            if (!tripPostIds.Any())
                return new List<ShipmentProposal>();

            return await _db.ShipmentProposals
                .Where(p => tripPostIds.Contains(p.TripPostId) && p.Status == ProposalStatusConstants.Pending)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<ShipmentProposal>> GetPendingByShipmentAsync(Guid shipmentId, CancellationToken ct)
        {
            return await _db.ShipmentProposals
                .Where(p => p.ShipmentId == shipmentId && p.Status == ProposalStatusConstants.Pending)
                .ToListAsync(ct);
        }

        public async Task<bool> HasAcceptedProposalForShipmentAsync(Guid shipmentId, CancellationToken ct)
        {
            return await _db.ShipmentProposals.AnyAsync(
                p => p.ShipmentId == shipmentId && p.Status == ProposalStatusConstants.Accepted,
                ct);
        }

        public async Task<bool> HasPendingProposalForShipmentAndTripPostAsync(
            Guid shipmentId, Guid tripPostId, CancellationToken ct)
        {
            return await _db.ShipmentProposals.AnyAsync(
                p => p.ShipmentId == shipmentId
                    && p.TripPostId == tripPostId
                    && p.Status == ProposalStatusConstants.Pending,
                ct);
        }

        // â”€â”€ Trip / Vehicle / Shipment Queries â”€â”€

        public async Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct)
        {
            return await _db.Trips.FirstOrDefaultAsync(
                t => t.DriverId == driverId && t.Status == "Active" && !t.IsDeleted,
                ct);
        }

        public async Task<Trip?> GetTripByIdAsync(Guid tripId, CancellationToken ct)
        {
            return await _db.Trips.FirstOrDefaultAsync(
                t => t.Id == tripId && !t.IsDeleted,
                ct);
        }

        public async Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct)
        {
            return await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        }

        public async Task<Shipment?> GetShipmentAsync(Guid shipmentId, CancellationToken ct)
        {
            return await _db.Shipments.FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        }

        public async Task<TripPostRecord?> GetTripPostAsync(Guid tripPostId, CancellationToken ct)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldOpen = connection.State != System.Data.ConnectionState.Open;
            if (shouldOpen) await connection.OpenAsync();

            try
            {
                using var cmd = connection.CreateCommand();
                if (_tx is not null)
                {
                    cmd.Transaction = _tx.GetDbTransaction();
                }
                cmd.CommandText = """
                    SELECT id, trip_id, created_by, title, description, accept_until,
                           status, pickup_mode, published_at, closed_at, created_at, updated_at
                    FROM transport.trip_posts
                    WHERE id = @id AND is_deleted = FALSE;
                """;
                var p = cmd.CreateParameter();
                p.ParameterName = "id";
                p.Value = tripPostId;
                cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new TripPostRecord
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        TripId = reader.GetGuid(reader.GetOrdinal("trip_id")),
                        CreatedBy = reader.GetGuid(reader.GetOrdinal("created_by")),
                        Title = reader.GetString(reader.GetOrdinal("title")),
                        Description = reader.IsDBNull(reader.GetOrdinal("description"))
                            ? null : reader.GetString(reader.GetOrdinal("description")),
                        AcceptUntil = reader.GetDateTime(reader.GetOrdinal("accept_until")),
                        Status = reader.GetString(reader.GetOrdinal("status")),
                        PickupMode = reader.GetString(reader.GetOrdinal("pickup_mode")),
                        PublishedAt = reader.IsDBNull(reader.GetOrdinal("published_at"))
                            ? null : reader.GetDateTime(reader.GetOrdinal("published_at")),
                        ClosedAt = reader.IsDBNull(reader.GetOrdinal("closed_at"))
                            ? null : reader.GetDateTime(reader.GetOrdinal("closed_at")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                    };
                }

                return null;
            }
            finally
            {
                if (shouldOpen) await connection.CloseAsync();
            }
        }

        // â”€â”€ Mutations â”€â”€

        public async Task AddAsync(ShipmentProposal proposal, CancellationToken ct)
        {
            await _db.ShipmentProposals.AddAsync(proposal, ct);
        }

        public async Task UpdateAsync(ShipmentProposal proposal, CancellationToken ct)
        {
            _db.ShipmentProposals.Update(proposal);
        }

        /// <summary>
        /// Atomically updates trip load using optimistic concurrency (Version).
        /// Throws DbUpdateConcurrencyException if the trip was modified by another transaction.
        /// </summary>
        public async Task UpdateTripLoadAsync(
            Guid tripId, decimal addWeight, decimal addVolume, int expectedVersion, CancellationToken ct)
        {
            var trip = await _db.Trips.FindAsync(new object[] { tripId }, ct)
                ?? throw new InvalidOperationException($"Trip {tripId} not found.");

            trip.CurrentLoadWeight += addWeight;
            trip.CurrentLoadVolume += addVolume;
            trip.Version += 1;

            _db.Trips.Update(trip);
        }

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            await _db.SaveChangesAsync(ct);
        }

        // â”€â”€ Transaction â”€â”€

        public Task BeginTransactionAsync(CancellationToken ct)
        {
            if (_db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                _tx = null;
                return Task.CompletedTask;
            }

            return BeginTransactionCoreAsync(ct);
        }

        private async Task BeginTransactionCoreAsync(CancellationToken ct)
        {
            _tx = await _db.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct)
        {
            if (_tx is not null)
            {
                await _tx.CommitAsync(ct);
                await _tx.DisposeAsync();
                _tx = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct)
        {
            if (_tx is not null)
            {
                await _tx.RollbackAsync(ct);
                await _tx.DisposeAsync();
                _tx = null;
            }
        }

        public System.Data.Common.DbConnection? GetUnderlyingConnection()
        {
            try
            {
                return _db.Database.GetDbConnection();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public System.Data.Common.DbTransaction? GetUnderlyingTransaction()
            => _tx?.GetDbTransaction();
    }
}
