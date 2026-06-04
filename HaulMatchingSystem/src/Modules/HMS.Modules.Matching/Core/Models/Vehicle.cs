using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Matching.Core.Models
{
    public class Vehicle
    {
        [Key]
        public Guid Id { get; set; }

        public decimal MaxWeightKg { get; set; }

        public decimal MaxVolumeCbm { get; set; }
    }
}
