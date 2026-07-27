namespace HMS.Modules.Matching.Application.DTOs
{
    /// <summary>
    /// Request to create a quotation for a proposal.
    /// </summary>
    public class CreateQuotationRequest
    {
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Request to update a quotation draft.
    /// </summary>
    public class UpdateQuotationRequest
    {
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Response after creating/updating a quotation.
    /// </summary>
    public class QuotationResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProposalId { get; set; }
        public string QuotationCode { get; set; } = string.Empty;
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public Guid? QuotedBy { get; set; }
        public DateTime? QuotedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
