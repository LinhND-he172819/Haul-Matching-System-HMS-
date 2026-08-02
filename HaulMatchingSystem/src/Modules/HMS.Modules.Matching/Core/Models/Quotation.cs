using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    /// <summary>
    /// Quotation created by Warehouse Staff for a shipment proposal.
    /// One proposal can have multiple quotations (history), but at most one Draft or Sent at a time.
    /// </summary>
    [Table("quotations", Schema = "warehouse")]
    public class Quotation
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("proposal_id")]
        public Guid ProposalId { get; set; }

        [Column("quotation_code")]
        public string QuotationCode { get; set; } = string.Empty;

        [Column("shipping_fee")]
        public decimal ShippingFee { get; set; }

        [Column("deposit_amount")]
        public decimal DepositAmount { get; set; }

        [Column("currency")]
        public string Currency { get; set; } = "VND";

        [Column("status")]
        public string Status { get; set; } = "Draft";

        [Column("quoted_by")]
        public Guid? QuotedBy { get; set; }

        [Column("quoted_at")]
        public DateTime? QuotedAt { get; set; }

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

        [Column("expired_at")]
        public DateTime? ExpiredAt { get; set; }

        [Column("cancelled_at")]
        public DateTime? CancelledAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        // Concurrency token
        [Timestamp]
        [Column("row_version")]
        public byte[]? RowVersion { get; set; }
    }
}
