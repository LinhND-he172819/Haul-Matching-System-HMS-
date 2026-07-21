using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    /// <summary>
    /// Represents a customer's proposal to include a Shipment in a specific Trip.
    /// A Shipment can have multiple proposals (one per TripPost), but at most one Accepted proposal.
    /// Pickup/sender info is stored here because different TripPosts may have different pickup locations.
    /// </summary>
    [Table("shipment_proposals", Schema = "warehouse")]
    public class ShipmentProposal
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("shipment_id")]
        public Guid ShipmentId { get; set; }

        [Column("trip_post_id")]
        public Guid TripPostId { get; set; }

        [Column("customer_id")]
        public Guid CustomerId { get; set; }

        // ── Sender / Pickup fields (per-proposal) ──
        [Column("sender_name")]
        public string SenderName { get; set; } = string.Empty;

        [Column("sender_phone")]
        public string SenderPhone { get; set; } = string.Empty;

        [Column("pickup_address")]
        public string PickupAddress { get; set; } = string.Empty;

        [Column("pickup_latitude")]
        public double? PickupLatitude { get; set; }

        [Column("pickup_longitude")]
        public double? PickupLongitude { get; set; }

        [Column("pickup_note")]
        public string? PickupNote { get; set; }

        // ── Status ──
        [Column("status")]
        public string Status { get; set; } = "Pending";

        // ── Timestamps ──
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

        [Column("accepted_by")]
        public Guid? AcceptedBy { get; set; }

        [Column("rejected_at")]
        public DateTime? RejectedAt { get; set; }

        [Column("rejected_by")]
        public Guid? RejectedBy { get; set; }

        [Column("reject_reason")]
        public string? RejectReason { get; set; }

        [Column("cancelled_at")]
        public DateTime? CancelledAt { get; set; }

        [Column("expired_at")]
        public DateTime? ExpiredAt { get; set; }
    }
}
