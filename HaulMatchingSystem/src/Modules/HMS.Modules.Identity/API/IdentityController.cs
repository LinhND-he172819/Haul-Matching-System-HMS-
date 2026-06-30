using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Identity.API
{
    [ApiController]
    [Route("api/identity")]
    public class IdentityController : ControllerBase
    {
        private readonly IdentityDbContext _context;

        public IdentityController(IdentityDbContext context)
        {
            _context = context;
        }

        [HttpGet("hubs")]
        public async Task<IActionResult> GetHubs()
        {
            var hubs = await _context.Hubs
                .Where(h => !h.IsDeleted)
                .Select(h => new { h.Id, h.Name, h.Address })
                .ToListAsync();

            return Ok(hubs);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await (from u in _context.Users
                               join v in _context.Vehicles on u.Id equals v.Id into vg
                               from v in vg.DefaultIfEmpty()
                               where !u.IsDeleted
                               select new UserDto
                               {
                                   Id = u.Id,
                                   FullName = u.FullName,
                                   Email = u.Email,
                                   Phone = u.Phone,
                                   Role = u.Role,
                                   HubId = u.HubId,
                                   CreatedAt = u.CreatedAt,
                                   LicensePlate = v != null ? v.LicensePlate : null,
                                   TruckType = v != null ? v.TruckType : null,
                                   MaxWeightKg = v != null ? v.MaxWeightKg : null,
                                   MaxVolumeCbm = v != null ? v.MaxVolumeCbm : null
                               }).ToListAsync();

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check duplicate email
            var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && !u.IsDeleted);
            if (emailExists)
            {
                return BadRequest(new { Message = "Email này đã được sử dụng trên hệ thống." });
            }

            // Check duplicate phone
            var phoneExists = await _context.Users.AnyAsync(u => u.Phone == dto.Phone && !u.IsDeleted);
            if (phoneExists)
            {
                return BadRequest(new { Message = "Số điện thoại này đã được sử dụng trên hệ thống." });
            }

            // Check hub exists if specified
            if (dto.HubId.HasValue)
            {
                var hubExists = await _context.Hubs.AnyAsync(h => h.Id == dto.HubId.Value && !h.IsDeleted);
                if (!hubExists)
                {
                    return BadRequest(new { Message = "Kho hàng trực thuộc (Hub) không hợp lệ hoặc không tồn tại." });
                }
            }
            else if (dto.Role == "Driver")
            {
                return BadRequest(new { Message = "Tài xế bắt buộc phải liên kết với một Kho hàng trực thuộc (Hub)." });
            }

            // Create User entity
            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                HubId = dto.HubId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Start Transaction to register user and vehicle atomically
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // If role is Driver, create and register vehicle
                if (dto.Role == "Driver")
                {
                    if (string.IsNullOrWhiteSpace(dto.LicensePlate))
                    {
                        return BadRequest(new { Message = "Tài xế yêu cầu đăng ký biển số xe tải." });
                    }

                    // Check duplicate license plate
                    var plateExists = await _context.Vehicles.AnyAsync(v => v.LicensePlate == dto.LicensePlate && !v.IsDeleted);
                    if (plateExists)
                    {
                        return BadRequest(new { Message = "Biển số xe này đã được đăng ký bởi tài xế khác." });
                    }

                    var vehicle = new Vehicle
                    {
                        Id = user.Id, // Link vehicle directly to DriverId
                        HubId = dto.HubId!.Value,
                        LicensePlate = dto.LicensePlate,
                        TruckType = dto.TruckType ?? "Xe Tải Nhẹ 1.5 Tấn",
                        MaxWeightKg = dto.MaxWeightKg ?? 1500,
                        MaxVolumeCbm = dto.MaxVolumeCbm ?? 8.5m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync();

                    // Synchronize vehicle details to identity.vehicles in Identity schema
                    await _context.Database.ExecuteSqlRawAsync(
                        "INSERT INTO identity.vehicles (\"Id\", \"MaxWeightKg\", \"MaxVolumeCbm\") VALUES ({0}, {1}, {2}) " +
                        "ON CONFLICT (\"Id\") DO UPDATE SET \"MaxWeightKg\" = {1}, \"MaxVolumeCbm\" = {2}",
                        user.Id, (decimal)(dto.MaxWeightKg ?? 1500), (decimal)(dto.MaxVolumeCbm ?? 8.5m)
                    );

                    // Create an Active trip for the new driver in public.trips
                    var activeTripId = Guid.NewGuid();
                    await _context.Database.ExecuteSqlRawAsync(
                        "INSERT INTO transport.trips (\"Id\", \"DriverId\", \"VehicleId\", \"CurrentLoadWeight\", \"CurrentLoadVolume\", \"Status\") " +
                        "VALUES ({0}, {1}, {2}, 0, 0, 'Active') " +
                        "ON CONFLICT (\"Id\") DO NOTHING",
                        activeTripId, user.Id, user.Id
                    );
                }

                await transaction.CommitAsync();

                var responseDto = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    HubId = user.HubId,
                    CreatedAt = user.CreatedAt,
                    LicensePlate = dto.Role == "Driver" ? dto.LicensePlate : null,
                    TruckType = dto.Role == "Driver" ? dto.TruckType : null,
                    MaxWeightKg = dto.Role == "Driver" ? dto.MaxWeightKg : null,
                    MaxVolumeCbm = dto.Role == "Driver" ? dto.MaxVolumeCbm : null
                };

                return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Lỗi trong quá trình xử lý lưu dữ liệu: " + ex.Message });
            }
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null)
            {
                return NotFound(new { Message = "Không tìm thấy người dùng." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                user.IsDeleted = true;
                user.UpdatedAt = DateTime.UtcNow;
                _context.Users.Update(user);

                if (user.Role == "Driver")
                {
                    var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
                    if (vehicle != null)
                    {
                        vehicle.IsDeleted = true;
                        vehicle.UpdatedAt = DateTime.UtcNow;
                        _context.Vehicles.Update(vehicle);
                    }

                    // Complete any active trips for the deleted driver
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE transport.trips SET \"Status\" = 'Completed' WHERE \"DriverId\" = {0} AND \"Status\" = 'Active'",
                        id
                    );
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { Message = "Xóa tài khoản thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Lỗi khi xóa tài khoản: " + ex.Message });
            }
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null)
            {
                return NotFound(new { Message = "Không tìm thấy người dùng." });
            }

            // Check duplicate email
            var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id && !u.IsDeleted);
            if (emailExists)
            {
                return BadRequest(new { Message = "Email này đã được sử dụng trên hệ thống." });
            }

            // Check duplicate phone
            var phoneExists = await _context.Users.AnyAsync(u => u.Phone == dto.Phone && u.Id != id && !u.IsDeleted);
            if (phoneExists)
            {
                return BadRequest(new { Message = "Số điện thoại này đã được sử dụng trên hệ thống." });
            }

            // Check hub exists if specified
            if (dto.HubId.HasValue)
            {
                var hubExists = await _context.Hubs.AnyAsync(h => h.Id == dto.HubId.Value && !h.IsDeleted);
                if (!hubExists)
                {
                    return BadRequest(new { Message = "Kho hàng trực thuộc (Hub) không hợp lệ hoặc không tồn tại." });
                }
            }
            else if (dto.Role == "Driver")
            {
                return BadRequest(new { Message = "Tài xế bắt buộc phải liên kết với một Kho hàng trực thuộc (Hub)." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                user.FullName = dto.FullName;
                user.Email = dto.Email;
                user.Phone = dto.Phone;
                user.HubId = dto.HubId;
                user.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(dto.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                if (user.Role == "Driver")
                {
                    var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
                    if (vehicle != null)
                    {
                        // Check duplicate license plate
                        var plateExists = await _context.Vehicles.AnyAsync(v => v.LicensePlate == dto.LicensePlate && v.Id != id && !v.IsDeleted);
                        if (plateExists)
                        {
                            return BadRequest(new { Message = "Biển số xe này đã được đăng ký bởi tài xế khác." });
                        }

                        vehicle.HubId = dto.HubId!.Value;
                        vehicle.LicensePlate = dto.LicensePlate ?? vehicle.LicensePlate;
                        vehicle.TruckType = dto.TruckType ?? vehicle.TruckType;
                        vehicle.MaxWeightKg = dto.MaxWeightKg ?? vehicle.MaxWeightKg;
                        vehicle.MaxVolumeCbm = dto.MaxVolumeCbm ?? vehicle.MaxVolumeCbm;
                        vehicle.UpdatedAt = DateTime.UtcNow;

                        _context.Vehicles.Update(vehicle);
                        await _context.SaveChangesAsync();

                        // Sync with identity.vehicles
                        await _context.Database.ExecuteSqlRawAsync(
                            "UPDATE identity.vehicles SET \"MaxWeightKg\" = {0}, \"MaxVolumeCbm\" = {1} WHERE \"Id\" = {2}",
                            (decimal)(dto.MaxWeightKg ?? 1500), (decimal)(dto.MaxVolumeCbm ?? 8.5m), id
                        );
                    }
                    else
                    {
                        var newVehicle = new Vehicle
                        {
                            Id = user.Id,
                            HubId = dto.HubId!.Value,
                            LicensePlate = dto.LicensePlate ?? "",
                            TruckType = dto.TruckType ?? "Xe Tải Nhẹ 1.5 Tấn",
                            MaxWeightKg = dto.MaxWeightKg ?? 1500,
                            MaxVolumeCbm = dto.MaxVolumeCbm ?? 8.5m,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        _context.Vehicles.Add(newVehicle);
                        await _context.SaveChangesAsync();

                        await _context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO identity.vehicles (\"Id\", \"MaxWeightKg\", \"MaxVolumeCbm\") VALUES ({0}, {1}, {2})",
                            user.Id, (decimal)(dto.MaxWeightKg ?? 1500), (decimal)(dto.MaxVolumeCbm ?? 8.5m)
                        );
                    }
                }

                await transaction.CommitAsync();

                var responseDto = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    HubId = user.HubId,
                    CreatedAt = user.CreatedAt,
                    LicensePlate = dto.Role == "Driver" ? dto.LicensePlate : null,
                    TruckType = dto.Role == "Driver" ? dto.TruckType : null,
                    MaxWeightKg = dto.Role == "Driver" ? dto.MaxWeightKg : null,
                    MaxVolumeCbm = dto.Role == "Driver" ? dto.MaxVolumeCbm : null
                };

                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Lỗi khi cập nhật thông tin: " + ex.Message });
            }
        }

    }
}
