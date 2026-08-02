namespace HMS.Modules.Matching.Core.Models
{
    /// <summary>
    /// Status constants for ShipmentProposal (string-based for DB storage).
    /// Note: There is also an enum HMS.Shared.Core.Enums.ProposalStatus used for typed comparisons.
    /// </summary>
    public static class ProposalStatusConstants
    {
        public const string PendingReview = "PendingReview";
        public const string Approved = "Approved";
        public const string Confirmed = "Confirmed";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
