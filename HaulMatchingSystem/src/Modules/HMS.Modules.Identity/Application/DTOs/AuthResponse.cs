using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
