using System.Threading;
using System.Threading.Tasks;

namespace HMS.Shared.Core.Interfaces
{
    public interface IDashboardStatsProvider
    {
        Task<(int activeTrips, int inTransitShipments, double avgUtilisation, int agingHubItems)> GetStatsAsync(CancellationToken ct);
    }
}
