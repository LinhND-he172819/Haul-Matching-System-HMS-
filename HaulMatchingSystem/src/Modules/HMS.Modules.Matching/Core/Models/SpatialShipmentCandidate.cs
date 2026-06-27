namespace HMS.Modules.Matching.Core.Models;

public sealed class SpatialShipmentCandidate
{
    public required Shipment Shipment { get; init; }
    public double RoutePosition { get; init; }
    public double DistanceMeters { get; init; }
}
