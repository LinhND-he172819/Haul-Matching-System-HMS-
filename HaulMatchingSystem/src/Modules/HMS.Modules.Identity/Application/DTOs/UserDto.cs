using System;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Role { get; set; } = null!;
        public Guid? HubId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Vehicle details if role is Driver
        public string? LicensePlate { get; set; }
        public string? TruckType { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public decimal? MaxVolumeCbm { get; set; }
    }
}
