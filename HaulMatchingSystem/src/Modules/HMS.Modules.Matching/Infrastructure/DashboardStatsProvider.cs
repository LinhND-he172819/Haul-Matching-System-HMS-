using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                int activeTrips = await _db.Trips.CountAsync(t => t.Status == "Active", ct);
                int inTransitShipments = await _db.Shipments.CountAsync(s => s.Status == "In_Transit" || s.Status == "Matched", ct);
                int agingHubItems = await _db.Shipments.CountAsync(s => s.Status == "In_Warehouse", ct);

                double avgUtilisation = 0;
                if (activeTrips > 0)
                {
                    var activeTripsList = await _db.Trips.Where(t => t.Status == "Active").ToListAsync(ct);
                    var vehicleIds = activeTripsList.Select(t => t.VehicleId).Distinct().ToList();
                    var vehicles = await _db.Vehicles.Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

                    double sum = 0;
                    int count = 0;
                    foreach (var t in activeTripsList)
                    {
                        if (vehicles.TryGetValue(t.VehicleId, out var v) && v.MaxWeightKg > 0)
                        {
                            var weightUtil = (double)(t.CurrentLoadWeight / v.MaxWeightKg) * 100;
                            var volUtil = v.MaxVolumeCbm > 0 ? (double)(t.CurrentLoadVolume / v.MaxVolumeCbm) * 100 : 0;
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
