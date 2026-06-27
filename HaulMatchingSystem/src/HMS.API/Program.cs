using FluentValidation;
using FluentValidation.AspNetCore;
using HMS.API.Middleware;
using HMS.Modules.Identity;
using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Interfaces;
using HMS.Modules.Identity.Infrastructure;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Infrastructure;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Services;
using HMS.Modules.Realtime.Workers;
using HMS.Modules.Matching.Infrastructure.Schema;
using HMS.Modules.Transport;
using HMS.Modules.Transport.Channels;
using HMS.Modules.Transport.Workers;
using HMS.Shared.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Đăng ký cấu hình CORS cho SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "SignalRPolicy",
        policy =>
        {
            //policy.WithOrigins("http://localhost:3000", "http://localhost:5173") // Domain của React/Vue Admin & App
            policy
                .SetIsOriginAllowed(origin => true) //test tạm thời, cho phép tất cả origin (không khuyến khích trong production)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Bắt buộc phải có để WebSocket hoạt động
        }
    );
});

// Đăng ký dịch vụ SignalR
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<HMS.Modules.Matching.Application.Validators.SelectedRequestValidator>();
builder.Services.Configure<JwtConfigs>(builder.Configuration.GetSection("Jwt"));
var jwtConfigs = builder.Configuration.GetSection("Jwt").Get<JwtConfigs>();

// DbContext (configure via env var or default sqlite for local dev)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(conn))
{
    conn = "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
}

builder.Services.AddDbContext<MatchingDbContext>(opt => opt.UseNpgsql(conn));
builder.Services.AddDbContext<IdentityDbContext>(opt => opt.UseNpgsql(conn));
builder.Services.AddScoped<IIdentityDbContext>(provider =>
    provider.GetRequiredService<IdentityDbContext>()
);
builder.Services.AddIdentityModule(builder.Configuration);

builder.Services.AddTransportModule(builder.Configuration);

// Redis
var redisConn = builder.Configuration.GetValue<string>("Redis:Connection") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConn)
);
builder.Services.AddScoped<IRedisLockService, RedisLockService>();

// Repos & services
builder.Services.AddScoped<IMatchingRepository, MatchingRepository>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddHttpClient<HMS.Shared.Core.Interfaces.ISmsService, HMS.Shared.Infrastructure.Services.SpeedSmsService>();
//builder.Services.AddSingleton<IMatchingSpatialSchemaInitializer, PostgresMatchingSpatialSchemaInitializer>();
builder.Services.AddScoped<
    HMS.Shared.Core.Interfaces.IDashboardStatsProvider,
    HMS.Modules.Matching.Infrastructure.DashboardStatsProvider
>();

// Exception middleware (registered as transient through pipeline)

//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = Path.ChangeExtension(
        System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "",
        ".xml"
    );
    if (File.Exists(xmlFile))
        c.IncludeXmlComments(xmlFile);
});

//builder.Services.ConfigureHttpJsonOptions(options =>
//{
//    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
//});

// Đăng ký Dispatcher
builder.Services.AddScoped<IRealtimeDispatcher, RealtimeDispatcher>();

// Đăng ký Background Worker để gửi số liệu Admin Dashboard
builder.Services.AddHostedService<DashboardStatsWorker>();
builder.Services.AddSingleton<GpsSyncChannel>();
builder.Services.AddHostedService<WriteBehindGpsWorker>();
builder.Services.AddHostedService<FleetMonitorWorker>();

// Đăng ký NullSmsSender để mock SMS trong môi trường phát triển
//builder.Services.AddScoped<ISmsSender, NullSmsSender>();

// Đăng ký dịch vụ SMS qua cổng API nội địa (Sẽ tự fallback về Mock nếu thiếu Key)
builder.Services.AddHttpClient<ISmsSender, VietNamSmsSender>();

builder.Services.AddHttpClient();
//----------------------------------------------------------------------------


var app = builder.Build();

await app.InitializeTransportModuleAsync();

await using (var scope = app.Services.CreateAsyncScope())
{
    //var initializer = scope.ServiceProvider.GetRequiredService<IMatchingSpatialSchemaInitializer>();
    //await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use exception middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Thứ tự chuẩn: 1. Https -> 2. Cors -> 3. Auth -> 4. Map Endpoints
app.UseCors("SignalRPolicy");
app.UseHttpsRedirection();

// Kích hoạt CORS


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map Endpoint tới Hub
app.MapHub<HmsFleetHub>("/hub/fleet");
app.MapTransportModule();

// Seed default hubs if database hubs table is empty
using (var scope = app.Services.CreateScope())
{
    try
    {
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        HMS.API.DbInitializer.Initialize(identityDb);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Lỗi khởi tạo database: " + ex.Message);
    }
}

app.Run();

