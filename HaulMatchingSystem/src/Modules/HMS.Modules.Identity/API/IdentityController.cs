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

        user.FullName = request.FullName.Trim();
        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.HubId = request.HubId;
        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(user));
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

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Xóa tài khoản thành công." });
    }

    private async Task<IActionResult?> ValidateUserRequestAsync(
        string email,
        string phone,
        string role,
        Guid? hubId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();
        var normalizedPhone = phone.Trim();

        var emailExists = await _context.Users.AnyAsync(
            user => user.Email == normalizedEmail &&
                    user.Id != currentUserId &&
                    !user.IsDeleted,
            cancellationToken);
        if (emailExists)
        {
            return BadRequest(new { Message = "Email này đã được sử dụng trên hệ thống." });
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
