using HMS.Shared.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Matching.Infrastructure;

public sealed class DashboardStatsProvider : IDashboardStatsProvider
{
    private readonly MatchingDbContext _db;

    public DashboardStatsProvider(MatchingDbContext db)
    {
        _db = db;
    }

    public async Task<(int activeTrips, int inTransitShipments, double avgUtilisation, int agingHubItems)> GetStatsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var activeTrips = await _db.Trips
                .AsNoTracking()
                .CountAsync(trip => trip.Status == "Active", cancellationToken);

            var inTransitShipments = await _db.Shipments
                .AsNoTracking()
                .CountAsync(
                    shipment => shipment.Status == "In_Transit" || shipment.Status == "Matched",
                    cancellationToken);

            var threeDaysAgo = DateTime.UtcNow.AddDays(-3);
            var agingHubItems = await _db.Shipments
                .AsNoTracking()
                .CountAsync(
                    shipment => shipment.Status == "In_Warehouse" && shipment.CreatedAt < threeDaysAgo,
                    cancellationToken);

            var activeTripLoads = await _db.Trips
                .AsNoTracking()
                .Where(trip => trip.Status == "Active")
                .Join(
                    _db.Vehicles.AsNoTracking(),
                    trip => trip.VehicleId,
                    vehicle => vehicle.Id,
                    (trip, vehicle) => new
                    {
                        trip.CurrentLoadWeight,
                        trip.CurrentLoadVolume,
                        vehicle.MaxWeightKg,
                        vehicle.MaxVolumeCbm
                    })
                .ToListAsync(cancellationToken);

            var utilisationValues = activeTripLoads
                .Where(item => item.MaxWeightKg > 0 && item.MaxVolumeCbm > 0)
                .Select(item =>
                {
                    var weight = (double)(item.CurrentLoadWeight / item.MaxWeightKg) * 100;
                    var volume = (double)(item.CurrentLoadVolume / item.MaxVolumeCbm) * 100;

                    return Math.Min(100, (weight + volume) / 2);
                })
                .ToArray();

            var averageUtilisation = utilisationValues.Length == 0
                ? 0
                : Math.Round(utilisationValues.Average(), 2);

            return (activeTrips, inTransitShipments, averageUtilisation, agingHubItems);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }
}
