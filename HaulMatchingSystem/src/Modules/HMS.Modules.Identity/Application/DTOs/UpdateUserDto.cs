using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Identity.Application.DTOs;

public sealed class UpdateUserDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = null!;

    [Required]
    [Phone]
    [MaxLength(20)]
    public string Phone { get; set; } = null!;

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MinLength(6)]
    [MaxLength(100)]
    public string? Password { get; set; }

    public Guid? HubId { get; set; }

    [Required]
    [RegularExpression("^(Customer|Driver|Warehouse_Staff|Admin)$", ErrorMessage = "Role must be Customer, Driver, Warehouse_Staff, or Admin")]
    public string Role { get; set; } = null!;

    // Vehicle details (Only required when Role is "Driver")
    [MaxLength(20)]
    public string? LicensePlate { get; set; }

    [MaxLength(50)]
    public string? TruckType { get; set; }

    public decimal? MaxWeightKg { get; set; }

    public decimal? MaxVolumeCbm { get; set; }
}
