using FluentValidation;
using FluentValidation.AspNetCore;
using HMS.API.Middleware;
using HMS.Modules.Identity.Application.DTOs;
using HMS.Modules.Identity.Core.Interfaces;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Infrastructure;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Interfaces;
using HMS.Modules.Realtime.Services;
using HMS.Modules.Realtime.Workers;
using HMS.Modules.Transport;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json.Serialization;

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
builder.Services.Configure<JwtConfigs>(builder.Configuration.GetSection("Jwt"));
var jwtConfigs = builder.Configuration.GetSection("Jwt").Get<JwtConfigs>();
// DbContext (configure via env var or default sqlite for local dev)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(conn))
{
    conn = "Host=localhost;Database=hms_matching;Username=postgres;Password=123";
}

builder.Services.AddDbContext<MatchingDbContext>(opt =>
    opt.UseNpgsql(conn)
);
builder.Services.AddScoped<IIdentityDbContext>(provider => provider.GetRequiredService<MatchingDbContext>());
// Redis
var redisConn = builder.Configuration.GetValue<string>("Redis:Connection") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddScoped<IRedisLockService, RedisLockService>();

// Repos & services
builder.Services.AddScoped<IMatchingRepository, MatchingRepository>();
builder.Services.AddScoped<IMatchingService, MatchingService>();

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

builder.Services.AddTransportModule();

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
app.MapTransportModule();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
