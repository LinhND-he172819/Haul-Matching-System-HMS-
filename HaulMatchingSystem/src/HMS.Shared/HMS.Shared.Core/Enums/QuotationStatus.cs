namespace HMS.Shared.Core.Enums;

/// <summary>
/// Status of a quotation for a shipment proposal.
/// </summary>
public enum QuotationStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Expired = 3,
    Cancelled = 4
}
