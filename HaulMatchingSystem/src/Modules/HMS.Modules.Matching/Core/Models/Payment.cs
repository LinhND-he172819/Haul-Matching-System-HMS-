using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    /// <summary>
    /// Payment transaction for a quotation.
    /// Supports Deposit, FinalPayment, AdditionalCharge, and Refund types.
    /// </summary>
    [Table("payments", Schema = "warehouse")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("quotation_id")]
        public Guid QuotationId { get; set; }

        [Column("shipment_id")]
        public Guid ShipmentId { get; set; }

        [Column("customer_id")]
        public Guid CustomerId { get; set; }

        [Column("payment_type")]
        public string PaymentType { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("currency")]
        public string Currency { get; set; } = "VND";

        [Column("payment_method")]
        public string? PaymentMethod { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("transaction_reference")]
        public string? TransactionReference { get; set; }

        [Column("idempotency_key")]
        public string? IdempotencyKey { get; set; }

        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }

        [Column("confirmed_at")]
        public DateTime? ConfirmedAt { get; set; }

        [Column("confirmed_by")]
        public Guid? ConfirmedBy { get; set; }

        [Column("failure_reason")]
        public string? FailureReason { get; set; }

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
