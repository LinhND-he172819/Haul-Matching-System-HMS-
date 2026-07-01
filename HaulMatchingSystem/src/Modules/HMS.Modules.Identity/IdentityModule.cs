using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Application.Services;
using HMS.Modules.Identity.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

namespace HMS.Modules.Identity
{
    public static class IdentityModule
    {
        public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Cấu hình và bind dữ liệu từ appsettings.json vào JwtConfigs
            var jwtSection = configuration.GetSection("Jwt");
            services.Configure<JwtConfigs>(jwtSection);

            var jwtConfigs = jwtSection.Get<JwtConfigs>();
            if (jwtConfigs == null || string.IsNullOrEmpty(jwtConfigs.Key))
            {
                throw new Exception("Lỗi: Cấu hình 'Jwt' trong appsettings.json bị thiếu hoặc không hợp lệ!");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfigs.Key));

            // 2. Cấu hình JWT Bearer Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityKey,

                    ValidateIssuer = true,
                    ValidIssuer = jwtConfigs.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtConfigs.Audience,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // Hết hạn là khóa ngay lập tức
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hub/fleet"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            // 3. Đăng ký các Service xử lý Logic bên trong Identity Module
            services.AddMemoryCache();
            services.AddScoped<IAuthService, AuthService>();

            // Quản lý thông tin User (Đăng ký, sửa thông tin, đổi mật khẩu, xóa user...)
            services.AddScoped<IUserService, UserService>();
            return services;
        }
    }
}