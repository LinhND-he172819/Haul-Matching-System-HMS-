using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Matching.Core.Models
{
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

        public string? Status { get; set; }
    }
}
