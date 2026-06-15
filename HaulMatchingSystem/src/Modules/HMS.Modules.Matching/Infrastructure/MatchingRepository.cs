using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HMS.Modules.Matching.Infrastructure
{
    public class MatchingRepository : IMatchingRepository
    {
        private readonly MatchingDbContext _db;
        private IDbContextTransaction? _tx;

        public MatchingRepository(MatchingDbContext db)
        {
            _db = db;
        }

        public async Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct)
        {
            return await _db.Trips.FirstOrDefaultAsync(t => t.DriverId == driverId && t.Status == "Active", ct);
        }

        public async Task<List<TripShipment>> GetSuggestedTripShipmentsAsync(Guid tripId, CancellationToken ct)
        {
            return await _db.TripShipments
                .Where(ts => ts.TripId == tripId && ts.Status == "Suggested")
                .OrderBy(ts => ts.DeliverySequence)
                .ToListAsync(ct);
        }

        public async Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct)
        {
            return await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        }

        public async Task<List<Shipment>> GetShipmentsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            return await _db.Shipments.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            await _db.SaveChangesAsync(ct);
        }

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
    }
}
