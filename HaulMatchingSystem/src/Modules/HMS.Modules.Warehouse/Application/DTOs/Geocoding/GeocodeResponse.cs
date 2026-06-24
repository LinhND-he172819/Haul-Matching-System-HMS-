namespace HMS.Modules.Warehouse.Application.DTOs.Geocoding;

public class GeocodeResponse
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}