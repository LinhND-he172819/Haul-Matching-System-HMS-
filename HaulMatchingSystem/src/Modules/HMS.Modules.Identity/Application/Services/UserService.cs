using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Core.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMS.Modules.Identity.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IIdentityDbContext _context;

        public UserService(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            // 1. Kiểm tra xem Email đã tồn tại trong hệ thống chưa
            var isEmailExists = await _context.Users
                .AnyAsync(u => u.Email == request.Email && !u.IsDeleted);

            if (isEmailExists)
            {
                throw new Exception("Email này đã được sử dụng bởi một tài khoản khác!");
            }

            // 2. Mã hóa mật khẩu (Sử dụng BCrypt rất an toàn)
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 3. Tạo Object User mới từ dữ liệu Request
            var newUser = new User { 
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Email = request.Email.ToLower().Trim(), // Chuẩn hóa chuỗi email viết thường
                PasswordHash = hashedPassword,
                Phone = request.Phone,
                Role = request.Role,
                HubId = request.HubId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

        // 4. Thêm vào DbContext và lưu xuống Database
        await _context.Users.AddAsync(newUser);
        var rowsAffected = await _context.SaveChangesAsync();

            // Trả về true nếu lưu thành công (> 0 bản ghi thay đổi)
            return rowsAffected > 0;
        }
}
}
