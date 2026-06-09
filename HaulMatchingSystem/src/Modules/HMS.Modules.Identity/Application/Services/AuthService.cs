using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HMS.Modules.Identity.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IIdentityDbContext _context; // Đổi tên DbContext của bạn cho đúng
        private readonly JwtConfigs _jwtConfigs;

        public AuthService(IIdentityDbContext context, IOptions<JwtConfigs> jwtConfigs)
        {
            _context = context;
            _jwtConfigs = jwtConfigs.Value;
        }

        // 1. Xử lý ĐĂNG NHẬP
        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            // Tìm user theo Email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);
            if (user == null) return null;

            
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid) return null;

            // Tạo cặp Token mới
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            // Lưu Refresh Token vào database
            int expiredDays = _jwtConfigs.ExpiredDate;
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(expiredDays);
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        // 2. Xử lý REFRESH TOKEN (Khi AccessToken hết hạn)
        public async Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            var principal = GetPrincipalFromExpiredToken(accessToken);
            if (principal == null) return null;

            // Lấy UserId từ Claims của AccessToken cũ
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId)) return null;

            var user = await _context.Users.FindAsync(userId);

            // Kiểm tra tính hợp lệ của RefreshToken
            if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow || user.IsDeleted)
            {
                return null; // Token không hợp lệ hoặc đã hết hạn
            }

            // Tạo chuỗi Token mới
            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Cập nhật lại Refresh Token mới vào DB (Xoay vòng token bảo mật)
            int expiredDays = _jwtConfigs.ExpiredDate;
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(expiredDays);
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

        #region Helper Methods (Hàm bổ trợ tự động tạo Token)

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtConfigs.Key);

            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

            if (user.HubId.HasValue)
            {
                claims.Add(new Claim("HubId", user.HubId.ToString()!));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtConfigs.TTL > 0 ? _jwtConfigs.TTL : 15), // Mặc định 15 phút nếu TTL lỗi
                Issuer = _jwtConfigs.Issuer,
                Audience = _jwtConfigs.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidIssuer = _jwtConfigs.Issuer,
                ValidAudience = _jwtConfigs.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfigs.Key)),
                ValidateLifetime = false // Quan trọng: Phải tắt kiểm tra hết hạn vì ta đang đọc Token đã ĐÃ HẾT HẠN
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
                return principal;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
