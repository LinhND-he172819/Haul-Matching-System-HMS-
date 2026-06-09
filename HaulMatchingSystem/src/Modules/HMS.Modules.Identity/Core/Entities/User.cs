using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public string Role { get; set; } = null!; // Nên map sang UserRole Enum trong C#

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
