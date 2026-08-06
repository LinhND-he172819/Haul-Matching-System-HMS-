namespace HMS.Shared.Core.Enums;

/// <summary>
/// Source of a shipment proposal — determines the creation flow and required fields.
/// Customer proposals require TripPostId; Driver proposals require RequestedTripId + DriverId.
/// </summary>
public enum ProposalSource
{
    Customer = 0,
    Driver = 1
}
