namespace HMS.Modules.Transport.Infrastructure.Routing;

public interface IHubLocationRepository
{
    Task<HubCoordinate?> GetCoordinateAsync(Guid hubId, CancellationToken cancellationToken = default);
}
