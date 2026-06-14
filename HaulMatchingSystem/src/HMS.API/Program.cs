using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Interfaces;
using HMS.Modules.Realtime.Services;
using HMS.Modules.Realtime.Workers;
using HMS.Modules.Matching.Infrastructure;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using HMS.API.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using HMS.Modules.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký cấu hình CORS cho SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        //policy.WithOrigins("http://localhost:3000", "http://localhost:5173") // Domain của React/Vue Admin & App
        policy.SetIsOriginAllowed(origin => true) //test tạm thời, cho phép tất cả origin (không khuyến khích trong production)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Bắt buộc phải có để WebSocket hoạt động
    });
});

// Đăng ký dịch vụ SignalR
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers();
// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<HMS.Modules.Matching.Application.Validators.SelectedRequestValidator>();

// DbContext (configure via env var or default sqlite for local dev)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(conn))
{
    conn = "Host=localhost;Database=hms_matching;Username=postgres;Password=postgres";
}

builder.Services.AddDbContext<MatchingDbContext>(opt =>
    opt.UseNpgsql(conn)
);

builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseNpgsql(conn)
);

// Redis
var redisConn = builder.Configuration.GetValue<string>("Redis:Connection") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddScoped<IRedisLockService, RedisLockService>();

// Repos & services
builder.Services.AddScoped<IMatchingRepository, MatchingRepository>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<HMS.Shared.Core.Interfaces.IDashboardStatsProvider, HMS.Modules.Matching.Infrastructure.DashboardStatsProvider>();

// Exception middleware (registered as transient through pipeline)

//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = System.IO.Path.ChangeExtension(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "", ".xml");
    if (System.IO.File.Exists(xmlFile)) c.IncludeXmlComments(xmlFile);
});

// Đăng ký Dispatcher
builder.Services.AddScoped<IRealtimeDispatcher, RealtimeDispatcher>();

// Đăng ký Background Worker để gửi số liệu Admin Dashboard
builder.Services.AddHostedService<DashboardStatsWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use exception middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Kích hoạt CORS
app.UseCors("SignalRPolicy");

app.UseAuthorization();

app.MapControllers();

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");



// Map Endpoint tới Hub
app.MapHub<HmsFleetHub>("/hub/fleet");

// Seed default hubs if database hubs table is empty
using (var scope = app.Services.CreateScope())
{
    try
    {
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!identityDb.Hubs.Any())
        {
            identityDb.Hubs.AddRange(
                new HMS.Modules.Identity.Core.Entities.Hub { Id = Guid.Parse("11111111-2222-3333-4444-555555555551"), Name = "Kho Gò Vấp - TP.HCM", Address = "12 Nguyễn Oanh, Gò Vấp, HCMC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new HMS.Modules.Identity.Core.Entities.Hub { Id = Guid.Parse("11111111-2222-3333-4444-555555555552"), Name = "Kho Tân Bình - TP.HCM", Address = "45 Cộng Hòa, Tân Bình, HCMC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new HMS.Modules.Identity.Core.Entities.Hub { Id = Guid.Parse("11111111-2222-3333-4444-555555555553"), Name = "Kho Hà Nội", Address = "102 Giải Phóng, Đống Đa, Hà Nội", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new HMS.Modules.Identity.Core.Entities.Hub { Id = Guid.Parse("11111111-2222-3333-4444-555555555554"), Name = "Kho Đà Nẵng", Address = "88 Nguyễn Lương Bằng, Liên Chiểu, Đà Nẵng", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            identityDb.SaveChanges();
            Console.WriteLine("Đã seed thành công 4 kho hàng mặc định vào database.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Lỗi seed dữ liệu Hub: " + ex.Message);
    }
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
