using System.Globalization;
using System.Text.Json;
using HMS.Modules.Warehouse.Application.DTOs.Geocoding;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Modules.Warehouse.Controllers;

[ApiController]
[Route("api/geocoding")]
public class GeocodingController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public GeocodingController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpPost("search")]
    public async Task<ActionResult<GeocodeResponse>> Search(
        [FromBody] GeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest("Address is required.");

        var url =
            "https://nominatim.openstreetmap.org/search" +
            $"?format=json&q={Uri.EscapeDataString(request.Address)}&limit=1";

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HaulMatchingSystem/1.0");

        var response = await _httpClient.GetStringAsync(url);

        using var json = JsonDocument.Parse(response);

        if (json.RootElement.GetArrayLength() == 0)
            return NotFound("Address not found.");

        var item = json.RootElement[0];

        var latText = item.GetProperty("lat").GetString();
        var lonText = item.GetProperty("lon").GetString();
        var displayName = item.GetProperty("display_name").GetString();

        return Ok(new GeocodeResponse
        {
            Lat = double.Parse(latText!, CultureInfo.InvariantCulture),
            Lng = double.Parse(lonText!, CultureInfo.InvariantCulture),
            DisplayName = displayName ?? request.Address
        });
    }
}