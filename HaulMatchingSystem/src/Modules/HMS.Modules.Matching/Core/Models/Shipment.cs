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

        [Column("receiver_name")]
        public string? ReceiverName { get; set; }

        [Column("receiver_phone")]
        public string? ReceiverPhone { get; set; }

        [Column("dest_address")]
        public string? DestAddress { get; set; }

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
