using System.Globalization;
using HMS.Modules.Transport.Application.Services;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Infrastructure.Routing;

public sealed class OsrmTripRoutePlanner(
    IHubLocationRepository hubLocationRepository,
    IOsrmRouteClient osrmRouteClient,
    ILogger<OsrmTripRoutePlanner> logger) : ITripRoutePlanner
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

        try
        {
            return await osrmRouteClient.GetRouteLineStringAsync(origin, destination, cancellationToken);
        }
        catch (RoutePlanningException ex)
        {
            // Fallback: generate a straight-line route when OSRM is unavailable
            logger.LogWarning(ex, "OSRM routing failed, falling back to straight-line route between hubs {Origin} and {Dest}",
                originHubId, destHubId);

            return BuildStraightLineString(origin, destination);
        }
    }

    private static string BuildStraightLineString(HubCoordinate origin, HubCoordinate destination)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"LINESTRING ({origin.Longitude:0.######} {origin.Latitude:0.######}, {destination.Longitude:0.######} {destination.Latitude:0.######})");
    }
}
