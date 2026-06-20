using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class LoginOtpRequest
    {
        [Required]
        public string Phone { get; set; } = null!;
        public string? Role { get; set; }
    }

    public class VerifyLoginOtpRequest
    {
        [Required]
        public string Phone { get; set; } = null!;
        [Required]
        public string Otp { get; set; } = null!;
        public string? Role { get; set; }
    }

    public class RegisterOtpRequest
    {
        [Required]
        public string Phone { get; set; } = null!;
    }

    public class VerifyRegisterOtpRequest
    {
        [Required]
        public string Phone { get; set; } = null!;
        [Required]
        public string FullName { get; set; } = null!;
        [Required]
        public string Otp { get; set; } = null!;
        public string Role { get; set; } = "Customer";
    }
}
