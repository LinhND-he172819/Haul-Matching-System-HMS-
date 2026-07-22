using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Identity.API;

[ApiController]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public IdentityController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet("hubs")]
    public async Task<IActionResult> GetHubs(CancellationToken cancellationToken)
    {
        var hubs = await _context.Hubs
            .AsNoTracking()
            .Where(hub => !hub.IsDeleted)
            .Select(hub => new { hub.Id, hub.Name, hub.Address })
            .ToListAsync(cancellationToken);

        return Ok(hubs);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted)
            .Select(user => new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                HubId = user.HubId,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await (from u in _context.Users
                          join v in _context.Vehicles on u.Id equals v.Id into vg
                          from v in vg.DefaultIfEmpty()
                          where u.Id == id && !u.IsDeleted
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
                          }).FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return NotFound(new { Message = "Không tìm thấy người dùng." });
        }

        return Ok(user);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await ValidateUserRequestAsync(
            request.Email,
            request.Phone,
            request.Role,
            request.HubId,
            null,
            cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            HubId = request.HubId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/identity/users/{user.Id}", ToDto(user));
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == id && !candidate.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound(new { Message = "Không tìm thấy người dùng." });
        }

        var validationResult = await ValidateUserRequestAsync(
            request.Email,
            request.Phone,
            request.Role,
            request.HubId,
            id,
            cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        // Block changing phone number
        if (request.Phone != user.Phone)
        {
            return BadRequest(new { Message = "Số điện thoại không được phép thay đổi." });
        }

        // Check hub exists if specified
        if (request.HubId.HasValue)
        {
            var hubExists = await _context.Hubs.AnyAsync(h => h.Id == request.HubId.Value && !h.IsDeleted, cancellationToken);
            if (!hubExists)
            {
                return BadRequest(new { Message = "Kho hàng trực thuộc (Hub) không hợp lệ hoặc không tồn tại." });
            }
        }
        else if (request.Role == "Driver")
        {
            return BadRequest(new { Message = "Tài xế bắt buộc phải liên kết với một Kho hàng trực thuộc (Hub)." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            user.FullName = request.FullName.Trim();
            user.Email = request.Email?.Trim();
            user.HubId = request.HubId;
            user.Role = request.Role;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync(cancellationToken);

            if (user.Role == "Driver")
            {
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
                if (vehicle != null)
                {
                    // Check duplicate license plate
                    var plateExists = await _context.Vehicles.AnyAsync(v => v.LicensePlate == request.LicensePlate && v.Id != id && !v.IsDeleted, cancellationToken);
                    if (plateExists)
                    {
                        return BadRequest(new { Message = "Biển số xe này đã được đăng ký bởi tài xế khác." });
                    }

                    vehicle.HubId = request.HubId!.Value;
                    vehicle.LicensePlate = request.LicensePlate ?? vehicle.LicensePlate;
                    vehicle.TruckType = request.TruckType ?? vehicle.TruckType;
                    vehicle.MaxWeightKg = request.MaxWeightKg ?? vehicle.MaxWeightKg;
                    vehicle.MaxVolumeCbm = request.MaxVolumeCbm ?? vehicle.MaxVolumeCbm;
                    vehicle.UpdatedAt = DateTime.UtcNow;

                    _context.Vehicles.Update(vehicle);
                    await _context.SaveChangesAsync(cancellationToken);

                    // Sync with public.vehicles
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE public.vehicles SET \"MaxWeightKg\" = {0}, \"MaxVolumeCbm\" = {1} WHERE \"Id\" = {2}",
                        (decimal)(request.MaxWeightKg ?? 1500), (decimal)(request.MaxVolumeCbm ?? 8.5m), id
                    );
                }
                else
                {
                    var newVehicle = new Vehicle
                    {
                        Id = user.Id,
                        HubId = request.HubId!.Value,
                        LicensePlate = request.LicensePlate ?? "",
                        TruckType = request.TruckType ?? "Xe Tải Nhẹ 1.5 Tấn",
                        MaxWeightKg = request.MaxWeightKg ?? 1500,
                        MaxVolumeCbm = request.MaxVolumeCbm ?? 8.5m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    _context.Vehicles.Add(newVehicle);
                    await _context.SaveChangesAsync(cancellationToken);

                    await _context.Database.ExecuteSqlRawAsync(
                        "INSERT INTO public.vehicles (\"Id\", \"MaxWeightKg\", \"MaxVolumeCbm\") VALUES ({0}, {1}, {2})",
                        user.Id, (decimal)(request.MaxWeightKg ?? 1500), (decimal)(request.MaxVolumeCbm ?? 8.5m)
                    );
                }
            }

            await transaction.CommitAsync(cancellationToken);

            var responseDto = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                HubId = user.HubId,
                CreatedAt = user.CreatedAt,
                LicensePlate = request.Role == "Driver" ? request.LicensePlate : null,
                TruckType = request.Role == "Driver" ? request.TruckType : null,
                MaxWeightKg = request.Role == "Driver" ? request.MaxWeightKg : null,
                MaxVolumeCbm = request.Role == "Driver" ? request.MaxVolumeCbm : null
            };

            return Ok(responseDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StatusCode(500, new { Message = "Lỗi khi cập nhật thông tin: " + ex.Message });
        }
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == id && !candidate.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound(new { Message = "Không tìm thấy người dùng." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);

            if (user.Role == "Driver")
            {
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
                if (vehicle != null)
                {
                    vehicle.IsDeleted = true;
                    vehicle.UpdatedAt = DateTime.UtcNow;
                    _context.Vehicles.Update(vehicle);
                }

                // Complete any active trips for the deleted driver
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE public.trips SET \"Status\" = 'Completed' WHERE \"DriverId\" = {0} AND \"Status\" = 'Active'",
                    id
                );
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return Ok(new { Message = "Xóa tài khoản thành công." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StatusCode(500, new { Message = "Lỗi khi xóa tài khoản: " + ex.Message });
        }
    }

    private async Task<IActionResult?> ValidateUserRequestAsync(
        string? email,
        string phone,
        string role,
        Guid? hubId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email?.Trim();
        var normalizedPhone = phone.Trim();

        if (!string.IsNullOrEmpty(normalizedEmail))
        {
            var emailExists = await _context.Users.AnyAsync(
                user => user.Email == normalizedEmail &&
                        user.Id != currentUserId &&
                        !user.IsDeleted,
                cancellationToken);
            if (emailExists)
            {
                return BadRequest(new { Message = "Email này đã được sử dụng trên hệ thống." });
            }
        }

        var phoneExists = await _context.Users.AnyAsync(
            user => user.Phone == normalizedPhone &&
                    user.Id != currentUserId &&
                    !user.IsDeleted,
            cancellationToken);
        if (phoneExists)
        {
            return BadRequest(new { Message = "Số điện thoại này đã được sử dụng trên hệ thống." });
        }

        if (role == "Driver" && !hubId.HasValue)
        {
            return BadRequest(new { Message = "Tài xế phải trực thuộc một Hub." });
        }

        if (hubId.HasValue)
        {
            var hubExists = await _context.Hubs.AnyAsync(
                hub => hub.Id == hubId.Value && !hub.IsDeleted,
                cancellationToken);
            if (!hubExists)
            {
                return BadRequest(new { Message = "Hub không tồn tại hoặc đã bị xóa." });
            }
        }

        return null;
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            HubId = user.HubId,
            CreatedAt = user.CreatedAt
        };
    }
}
