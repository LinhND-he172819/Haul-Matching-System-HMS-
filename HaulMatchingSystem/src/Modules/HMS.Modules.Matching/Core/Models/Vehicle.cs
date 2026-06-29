using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("vehicles", Schema = "transport")]
    public class Vehicle
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("max_weight_kg")]
        public decimal MaxWeightKg { get; set; }

        [Column("max_volume_cbm")]
        public decimal MaxVolumeCbm { get; set; }
    }
}
