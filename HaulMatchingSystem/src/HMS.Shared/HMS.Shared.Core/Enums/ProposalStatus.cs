namespace HMS.Shared.Core.Enums;

/// <summary>
/// Status of a shipment proposal (warehouse.shipment_proposals).
/// </summary>
public enum ProposalStatus
{
    PendingReview = 0,
    Approved = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Expired = 5
}
