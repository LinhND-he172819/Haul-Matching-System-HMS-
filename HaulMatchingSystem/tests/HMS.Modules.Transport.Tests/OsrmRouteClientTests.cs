using System.Net;
using System.Text;
using HMS.Modules.Transport.Infrastructure.Routing;
using Microsoft.Extensions.Configuration;

namespace HMS.Modules.Transport.Tests;

public sealed class OsrmRouteClientTests
{
    [Fact]
    public async Task GetRouteLineStringAsync_ConvertsOsrmGeoJsonRouteToWktLineString()
    {
        var handler = new StubHttpMessageHandler("""
            {
              "code": "Ok",
              "routes": [
                {
                  "geometry": {
                    "coordinates": [
                      [106.7, 10.8],
                      [107.58, 11.94],
                      [108.22, 16.07]
                    ]
                  }
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://osrm.local/")
        };
        var configuration = new ConfigurationManager
        {
            ["Osrm:Profile"] = "driving"
        };
        var client = new OsrmRouteClient(httpClient, configuration);

        var routeLineString = await client.GetRouteLineStringAsync(
            new HubCoordinate(Guid.NewGuid(), 106.7, 10.8),
            new HubCoordinate(Guid.NewGuid(), 108.22, 16.07));

        Assert.Equal("LINESTRING (106.7 10.8, 107.58 11.94, 108.22 16.07)", routeLineString);
        Assert.NotNull(handler.RequestUri);
        Assert.Contains("route/v1/driving/106.7,10.8;108.22,16.07", handler.RequestUri.ToString());
        Assert.Contains("geometries=geojson", handler.RequestUri.ToString());
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
