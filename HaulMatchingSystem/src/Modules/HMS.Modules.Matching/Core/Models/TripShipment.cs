using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("trip_shipments", Schema = "transport")]
    public class TripShipment
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Column("shipment_id")]
        public Guid ShipmentId { get; set; }

        [Column("delivery_sequence")]
        public int DeliverySequence { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("suggested_at")]
        public DateTime? SuggestedAt { get; set; }

        [Column("responded_at")]
        public DateTime? RespondedAt { get; set; }

        [Column("responded_by")]
        public Guid? RespondedBy { get; set; }
    }
}
