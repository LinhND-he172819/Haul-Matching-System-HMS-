using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; } = null!;

        public string? Phone { get; set; }

        // Mặc định nếu không truyền Role sẽ là "User", hoặc truyền "Driver", "Admin" tùy nhu cầu
        public string Role { get; set; } = "User";

        public Guid? HubId { get; set; }
    }
}
