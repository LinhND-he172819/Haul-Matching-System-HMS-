using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    public class Trip
    {
        [Key]
        public Guid Id { get; set; }

        public Guid DriverId { get; set; }

        public Guid VehicleId { get; set; }

        public decimal CurrentLoadWeight { get; set; }

        public decimal CurrentLoadVolume { get; set; }

        public string? Status { get; set; }

        [Timestamp]
        public byte[]? Version { get; set; }
    }
}
