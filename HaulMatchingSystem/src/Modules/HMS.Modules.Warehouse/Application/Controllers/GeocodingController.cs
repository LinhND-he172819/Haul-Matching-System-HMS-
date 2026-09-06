using System.Globalization;
using System.Text.Json;
using HMS.Modules.Warehouse.Application.DTOs.Geocoding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/geocoding")]
public class GeocodingController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeocodingController> _logger;

    // Retry configuration
    private const int MaxRetries = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public GeocodingController(IHttpClientFactory httpClientFactory, ILogger<GeocodingController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("search")]
    public async Task<ActionResult<GeocodeResponse>> Search(
        [FromBody] GeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest("Address is required.");

        _logger.LogInformation("Geocoding request for address: {Address}", request.Address);

        // Try Photon first (more reliable, faster)
        var photonResult = await SearchPhotonAsync(request.Address);
        if (photonResult != null)
        {
            _logger.LogInformation("Photon geocoding succeeded for: {Address}", request.Address);
            return Ok(photonResult);
        }

        // Fallback to Nominatim
        _logger.LogWarning("Photon failed, falling back to Nominatim for: {Address}", request.Address);
        var nominatimResult = await SearchNominatimAsync(request.Address);
        if (nominatimResult != null)
        {
            _logger.LogInformation("Nominatim geocoding succeeded for: {Address}", request.Address);
            return Ok(nominatimResult);
        }

        // Both providers failed
        _logger.LogWarning("All geocoding providers failed for address: {Address}", request.Address);
        return NotFound(new { error = "Not found", message = "Không tìm thấy địa chỉ. Vui lòng nhập địa chỉ rõ hơn." });
    }

    /// <summary>
    /// Photon (Komoot) — primary provider, faster & more reliable.
    /// Response format: GeoJSON FeatureCollection.
    /// </summary>
    private async Task<GeocodeResponse?> SearchPhotonAsync(string address)
    {
        var url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(address)}&limit=1";

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Photon returned {StatusCode} (attempt {Attempt})", response.StatusCode, attempt + 1);
                    if (attempt < MaxRetries) await Task.Delay(RetryDelay);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var features = doc.RootElement.GetProperty("features");
                if (features.GetArrayLength() == 0)
                    return null; // No results — don't retry

                var feature = features[0];
                var geometry = feature.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates"); // [lng, lat] in GeoJSON

                var lng = coordinates[0].GetDouble();
                var lat = coordinates[1].GetDouble();

                // Build display name from properties
                var props = feature.GetProperty("properties");
                var displayName = BuildDisplayName(props) ?? address;

                return new GeocodeResponse
                {
                    Lat = lat,
                    Lng = lng,
                    DisplayName = displayName
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Photon attempt {Attempt} failed for: {Address}", attempt + 1, address);
                if (attempt < MaxRetries) await Task.Delay(RetryDelay);
            }
        }

        return null;
    }

    /// <summary>
    /// Build a human-readable display name from Photon properties.
    /// </summary>
    private static string BuildDisplayName(JsonElement props)
    {
        var parts = new List<string>();

        if (props.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } n)
            parts.Add(n);

        if (props.TryGetProperty("city", out var city) && city.GetString() is { Length: > 0 } c)
            parts.Add(c);

        if (props.TryGetProperty("state", out var state) && state.GetString() is { Length: > 0 } s)
            parts.Add(s);

        if (props.TryGetProperty("country", out var country) && country.GetString() is { Length: > 0 } co)
            parts.Add(co);

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    /// <summary>
    /// Nominatim — fallback provider.
    /// Response format: JSON array of objects with lat/lon strings.
    /// </summary>
    private async Task<GeocodeResponse?> SearchNominatimAsync(string address)
    {
        var url =
            "https://nominatim.openstreetmap.org/search" +
            $"?format=json&q={Uri.EscapeDataString(address)}&limit=1";

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("HaulMatchingSystem/1.0 (haul-matching-system)");

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim returned {StatusCode} (attempt {Attempt})", response.StatusCode, attempt + 1);
                    if (attempt < MaxRetries) await Task.Delay(RetryDelay);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.GetArrayLength() == 0)
                    return null; // No results — don't retry

                var item = doc.RootElement[0];

                var latText = item.GetProperty("lat").GetString();
                var lonText = item.GetProperty("lon").GetString();
                var displayName = item.GetProperty("display_name").GetString();

                return new GeocodeResponse
                {
                    Lat = double.Parse(latText!, CultureInfo.InvariantCulture),
                    Lng = double.Parse(lonText!, CultureInfo.InvariantCulture),
                    DisplayName = displayName ?? address
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nominatim attempt {Attempt} failed for: {Address}", attempt + 1, address);
                if (attempt < MaxRetries) await Task.Delay(RetryDelay);
            }
        }

        return null;
    }
}