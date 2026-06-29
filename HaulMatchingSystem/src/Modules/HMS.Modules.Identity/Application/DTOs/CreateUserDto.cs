using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Identity.Application.DTOs
{
    public class CreateUserDto
    {
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = null!;

        [Required]
        [Phone]
        [MaxLength(20)]
        public string Phone { get; set; } = null!;

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string Password { get; set; } = null!;

        public Guid? HubId { get; set; }

        [Required]
        [RegularExpression("^(Customer|Driver|Warehouse_Staff|Admin)$", ErrorMessage = "Role must be Customer, Driver, Warehouse_Staff, or Admin")]
        public string Role { get; set; } = null!;

    }
}
