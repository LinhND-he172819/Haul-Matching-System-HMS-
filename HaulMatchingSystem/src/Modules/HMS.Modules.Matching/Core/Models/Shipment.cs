using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("shipments", Schema = "warehouse")]
    public class Shipment
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        // ── Sender fields (used for DirectPickup) ──
        [Column("sender_name")]
        public string? SenderName { get; set; }

        [Column("sender_phone")]
        public string? SenderPhone { get; set; }

        [Column("pickup_address")]
        public string? PickupAddress { get; set; }

        [Column("pickup_latitude")]
        public double? PickupLatitude { get; set; }

        [Column("pickup_longitude")]
        public double? PickupLongitude { get; set; }

        [Column("pickup_note")]
        public string? PickupNote { get; set; }

        [Column("picked_up_at")]
        public DateTime? PickedUpAt { get; set; }

        [Column("picked_up_by")]
        public Guid? PickedUpBy { get; set; }

        // ── Receiver fields ──
        [Column("receiver_name")]
        public string? ReceiverName { get; set; }

        [Column("receiver_phone")]
        public string? ReceiverPhone { get; set; }

        [Column("dest_address")]
        public string? DestAddress { get; set; }

        // ── Cargo fields ──
        [Column("weight_kg")]
        public decimal WeightKg { get; set; }

        [Column("volume_cbm")]
        public decimal VolumeCbm { get; set; }

        [Column("cargo_type")]
        public string? CargoType { get; set; }

        [Column("special_handling_note")]
        public string? SpecialHandlingNote { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
