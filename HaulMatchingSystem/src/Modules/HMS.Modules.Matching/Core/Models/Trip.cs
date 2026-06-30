using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("trips", Schema = "transport")]
    public class Trip
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("driver_id")]
        public Guid DriverId { get; set; }

        [Column("vehicle_id")]
        public Guid VehicleId { get; set; }

        [Column("current_load_weight")]
        public decimal CurrentLoadWeight { get; set; }

        [Column("current_load_volume")]
        public decimal CurrentLoadVolume { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("version")]
        public int Version { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }
}
