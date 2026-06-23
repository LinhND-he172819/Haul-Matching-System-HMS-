using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("shipments", Schema = "warehouse")]
    public class Shipment
    {
        [Key]
        public Guid Id { get; set; }

        public string? ReceiverName { get; set; }

        public string? ReceiverPhone { get; set; }

        public string? DestAddress { get; set; }

        public decimal WeightKg { get; set; }

        public decimal VolumeCbm { get; set; }

        public string? CargoType { get; set; }

        public string? SpecialHandlingNote { get; set; }
        [Column("status")]
        public string? Status { get; set; }
    }
}
