using HMS.Shared.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Matching.Infrastructure
{
    public class DashboardStatsProvider : IDashboardStatsProvider
    {
        private readonly MatchingDbContext _db;

        public DashboardStatsProvider(MatchingDbContext db)
        {
            _db = db;
        }

        public async Task<(int activeTrips, int inTransitShipments, double avgUtilisation, int agingHubItems)> GetStatsAsync(CancellationToken ct)
        {
            try
            {
                int activeTrips = await _db.Trips.AsNoTracking().CountAsync(t => t.Status == "Active", ct);
                int inTransitShipments = await _db.Shipments.AsNoTracking()
                    .CountAsync(s => s.Status == "In_Transit" || s.Status == "Matched", ct);
                var threeDaysAgo = DateTime.UtcNow.AddDays(-3);
                int agingHubItems = await _db.Shipments.AsNoTracking()
                    .CountAsync(s => s.Status == "In_Warehouse" && s.CreatedAt < threeDaysAgo, ct);

                double avgUtilisation = 0;
                if (activeTrips > 0)
                {
                    var tripUtils = await _db.Trips.AsNoTracking()
                        .Where(t => t.Status == "Active")
                        .Join(_db.Vehicles.AsNoTracking(),
                            t => t.VehicleId,
                            v => v.Id,
                            (t, v) => new
                            {
                                t.CurrentLoadWeight,
                                t.CurrentLoadVolume,
                                v.MaxWeightKg,
                                v.MaxVolumeCbm
                            })
                        .ToListAsync(ct);                    

                    double sum = 0;
                    int count = 0;
                    foreach (var item in tripUtils)
                    {
                        if (item.MaxWeightKg > 0)
                        {
                            var weightUtil = (double)(item.CurrentLoadWeight / item.MaxWeightKg) * 100;
                            var volUtil = item.MaxVolumeCbm > 0 ? (double)(item.CurrentLoadVolume / item.MaxVolumeCbm) * 100 : 0;
                            var util = (weightUtil + volUtil) / 2;
                            sum += Math.Min(100.0, util);
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        avgUtilisation = Math.Round(sum / count, 2);
                    }
                }

                return (activeTrips, inTransitShipments, avgUtilisation, agingHubItems);
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }
    }
}
