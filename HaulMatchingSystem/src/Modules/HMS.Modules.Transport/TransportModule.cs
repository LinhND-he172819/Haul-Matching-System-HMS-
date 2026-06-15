using HMS.Modules.Transport.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HMS.Modules.Transport;

public static class TransportModule
{
    // Gom toàn bộ cấu hình của module Transport vào 1 chỗ
    public static IServiceCollection AddTransportModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Đăng ký Database của riêng module Transport
        services.AddDbContext<TransportDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // 2. Đăng ký các Services (nếu có sau này)
        return services;
    }
}
