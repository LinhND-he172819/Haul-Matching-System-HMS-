using System;

namespace HMS.Modules.Identity.Core.Entities
{
    public class Vehicle
    {
        public Guid Id { get; set; }
        public Guid HubId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string TruckType { get; set; } = null!;
        public decimal MaxWeightKg { get; set; }
        public decimal MaxVolumeCbm { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}
