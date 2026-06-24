using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Matching.Core.Models
{
    [Table("vehicles", Schema = "identity")]
    public class Vehicle
    {
        [Key]
        public Guid Id { get; set; }

        public decimal MaxWeightKg { get; set; }

        public decimal MaxVolumeCbm { get; set; }
    }
}
