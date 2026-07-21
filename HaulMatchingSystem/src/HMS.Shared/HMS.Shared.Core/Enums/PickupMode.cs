namespace HMS.Shared.Core.Enums;

/// <summary>
/// Determines how a shipment is picked up — either the customer brings it to a Hub,
/// or the driver goes to the sender's address for direct pickup.
/// Stored on TripPost (Single Source of Truth).
/// </summary>
public enum PickupMode
{
    Hub = 0,
    DirectPickup = 1
}
