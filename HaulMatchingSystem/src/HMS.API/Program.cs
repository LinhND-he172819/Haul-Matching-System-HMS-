using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Interfaces;
using HMS.Modules.Realtime.Services;
using HMS.Modules.Realtime.Workers;

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

//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Kích hoạt CORS
app.UseCors("SignalRPolicy");

// Map Endpoint tới Hub
app.MapHub<HmsFleetHub>("/hub/fleet");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
