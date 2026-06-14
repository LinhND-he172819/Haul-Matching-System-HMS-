using System;

namespace HMS.Modules.Identity.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public Guid? HubId { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? GoogleId { get; set; }
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetTokenExpiresAt { get; set; }
        public string Role { get; set; } = null!; // "Admin", "Warehouse_Staff", "Driver", "Customer"
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}
