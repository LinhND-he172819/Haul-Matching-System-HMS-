namespace HMS.Modules.Transport.Infrastructure.Routing;

public interface IOsrmRouteClient
{
    Task<string> GetRouteLineStringAsync(
        HubCoordinate origin,
        HubCoordinate destination,
        CancellationToken cancellationToken = default);
}
