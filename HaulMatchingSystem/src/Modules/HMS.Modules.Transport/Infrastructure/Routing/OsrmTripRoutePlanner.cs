using HMS.Modules.Transport.Application.Services;

namespace HMS.Modules.Transport.Infrastructure.Routing;

public sealed class OsrmTripRoutePlanner(
    IHubLocationRepository hubLocationRepository,
    IOsrmRouteClient osrmRouteClient) : ITripRoutePlanner
{
    public async Task<string> ResolveRouteLineStringAsync(
        Guid originHubId,
        Guid destHubId,
        string? requestedRouteLineString,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requestedRouteLineString))
        {
            return requestedRouteLineString.Trim();
        }

        if (originHubId == Guid.Empty)
        {
            throw new ArgumentException("OriginHubId is required.", nameof(originHubId));
        }

        if (destHubId == Guid.Empty)
        {
            throw new ArgumentException("DestHubId is required.", nameof(destHubId));
        }

        var origin = await hubLocationRepository.GetCoordinateAsync(originHubId, cancellationToken)
            ?? throw new ArgumentException("Origin hub location was not found.", nameof(originHubId));

        var destination = await hubLocationRepository.GetCoordinateAsync(destHubId, cancellationToken)
            ?? throw new ArgumentException("Destination hub location was not found.", nameof(destHubId));

        return await osrmRouteClient.GetRouteLineStringAsync(origin, destination, cancellationToken);
    }
}
