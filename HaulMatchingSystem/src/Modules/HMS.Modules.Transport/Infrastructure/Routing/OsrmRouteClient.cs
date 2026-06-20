using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using HMS.Modules.Transport.Application.Services;
using Microsoft.Extensions.Configuration;

namespace HMS.Modules.Transport.Infrastructure.Routing;

public sealed class OsrmRouteClient : IOsrmRouteClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _profile;

    public OsrmRouteClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _profile = configuration.GetValue<string>("Osrm:Profile") ?? "driving";
    }

    public async Task<string> GetRouteLineStringAsync(
        HubCoordinate origin,
        HubCoordinate destination,
        CancellationToken cancellationToken = default)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"route/v1/{_profile}/{origin.Longitude},{origin.Latitude};{destination.Longitude},{destination.Latitude}?overview=full&geometries=geojson");

        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new RoutePlanningException("OSRM could not find a road-network route for the selected hubs.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new RoutePlanningException(
                $"OSRM route request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var routeResponse = await JsonSerializer.DeserializeAsync<OsrmRouteResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        var coordinates = routeResponse?.Routes?.FirstOrDefault()?.Geometry?.Coordinates;
        if (!string.Equals(routeResponse?.Code, "Ok", StringComparison.OrdinalIgnoreCase)
            || coordinates is null
            || coordinates.Count < 2)
        {
            throw new RoutePlanningException("OSRM returned an empty route geometry for the selected hubs.");
        }

        return ToLineString(coordinates);
    }

    private static string ToLineString(IReadOnlyCollection<double[]> coordinates)
    {
        var points = coordinates.Select(coordinate =>
        {
            if (coordinate.Length < 2)
            {
                throw new RoutePlanningException("OSRM returned an invalid coordinate pair.");
            }

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{coordinate[0]:0.######} {coordinate[1]:0.######}");
        });

        return $"LINESTRING ({string.Join(", ", points)})";
    }

    private sealed class OsrmRouteResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("routes")]
        public List<OsrmRoute>? Routes { get; init; }
    }

    private sealed class OsrmRoute
    {
        [JsonPropertyName("geometry")]
        public OsrmGeometry? Geometry { get; init; }
    }

    private sealed class OsrmGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<double[]> Coordinates { get; init; } = [];
    }
}
