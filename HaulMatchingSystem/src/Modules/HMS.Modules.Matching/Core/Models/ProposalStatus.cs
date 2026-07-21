namespace HMS.Modules.Matching.Core.Models
{
    /// <summary>
    /// Status constants for ShipmentProposal (string-based for DB storage).
    /// Note: There is also an enum HMS.Shared.Core.Enums.ProposalStatus used for typed comparisons.
    /// </summary>
    public static class ProposalStatusConstants
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
