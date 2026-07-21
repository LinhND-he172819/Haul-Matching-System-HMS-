namespace HMS.Shared.Core.Enums;

/// <summary>
/// Status of a shipment proposal (trip_shipments row created by a customer).
/// </summary>
public enum ProposalStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3,
    Expired = 4
}
