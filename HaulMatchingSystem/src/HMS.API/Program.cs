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
using HMS.Modules.Matching.Infrastructure.Schema;
using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Services;
using HMS.Modules.Realtime.Workers;
using HMS.Modules.Telemetry;
using HMS.Modules.Telemetry.Endpoints;
using HMS.Modules.Transport;
using HMS.Modules.Transport.Channels;
using HMS.Modules.Transport.Workers;
using HMS.Modules.Warehouse.Application.Services;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all network interfaces 
builder.WebHost.UseUrls("http://0.0.0.0:5104");

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
builder.Services.AddControllers()
    .AddApplicationPart(typeof(HMS.Modules.Transport.Controllers.TripPostsController).Assembly)
    .AddApplicationPart(typeof(HMS.Modules.Warehouse.Controllers.DriverTripController).Assembly)
    .AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
// Modules registration
builder.Services.AddIdentityModule(builder.Configuration);

builder.Services.AddTransportModule(builder.Configuration);

builder.Services.AddScoped<IHubInventoryService, HubInventoryService>();

// Shipment State Machine
builder.Services.AddScoped<IShipmentStateService, ShipmentStateService>();
builder.Services.AddScoped<HMS.Modules.Warehouse.Application.Services.PostgresWarehouseSchemaInitializer>();
builder.Services.AddScoped<HMS.Modules.Warehouse.Application.Services.CustomerDriverSchemaInitializer>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(HMS.Shared.Core.Events.ShipmentStatusChangedEvent).Assembly));
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(HMS.Modules.Warehouse.Application.Services.ShipmentStateService).Assembly));

builder.Services.AddTelemetryModule();

// Redis
var redisConn = builder.Configuration.GetValue<string>("Redis:Connection") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConn)
);
builder.Services.AddScoped<IRedisLockService, RedisLockService>();

// Repos & services
builder.Services.AddScoped<IMatchingRepository, MatchingRepository>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
//builder.Services.AddHttpClient<HMS.Shared.Core.Interfaces.ISmsService, HMS.Shared.Infrastructure.Services.SpeedSmsService>();
builder.Services.AddSingleton<IMatchingSpatialSchemaInitializer, PostgresMatchingSpatialSchemaInitializer>();
builder.Services.AddScoped<
    HMS.Shared.Core.Interfaces.IDashboardStatsProvider,
    HMS.Modules.Matching.Infrastructure.DashboardStatsProvider
>();

// Matching module — Proposal / Quotation / Payment services
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IProposalRepository, HMS.Modules.Matching.Infrastructure.ProposalRepository>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IProposalService, HMS.Modules.Matching.Application.Services.ProposalService>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IStaffProposalService, HMS.Modules.Matching.Application.Services.StaffProposalService>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IDriverExternalShipmentService, HMS.Modules.Matching.Application.Services.DriverExternalShipmentService>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IQuotationService, HMS.Modules.Matching.Application.Services.QuotationService>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IPaymentService, HMS.Modules.Matching.Application.Services.PaymentService>();
builder.Services.AddScoped<HMS.Modules.Matching.Core.Interfaces.IQuotationPaymentRepository, HMS.Modules.Matching.Infrastructure.QuotationPaymentRepository>();

// Incident Management services
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IncidentService>();

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
builder.Services.AddHostedService<TripPostExpiryWorker>();

// Đăng ký NullSmsSender để mock SMS trong môi trường phát triển
//builder.Services.AddScoped<ISmsSender, NullSmsSender>();

// Đăng ký dịch vụ SMS qua cổng API nội địa (Sẽ tự fallback về Mock nếu thiếu Key)
builder.Services.AddHttpClient<ISmsSender, VietNamSmsSender>();

builder.Services.AddHttpClient();
//----------------------------------------------------------------------------


var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// Schema init order — FK dependency chain (BẮT BUỘC thứ tự này):
//   1. Identity  → no deps
//   2. Warehouse → warehouse.shipments, shipment_proposals
//   3. Transport → FK → warehouse.shipments, warehouse.shipment_proposals
//   4. Customer/Driver → FK → warehouse.* + transport.trips
//   5. Matching indexes → warehouse.* columns
// ══════════════════════════════════════════════════════════════

// Step 1: Identity
using (var scope = app.Services.CreateScope())
{
    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    HMS.API.DbInitializer.Initialize(identityDb);
}

// Step 2: Warehouse (shipments, shipment_proposals, shipment_status_history)
await using (var whScope = app.Services.CreateAsyncScope())
{
    var whInitializer = whScope.ServiceProvider
        .GetRequiredService<HMS.Modules.Warehouse.Application.Services.PostgresWarehouseSchemaInitializer>();
    await whInitializer.InitializeAsync();
}

// Step 3: Transport (vehicles, trips, trip_shipments FK → warehouse.*, gps_logs…)
await app.InitializeTransportModuleAsync();

// Step 4: Customer/Driver (quotations, payments, trip_incidents FK → transport.trips, audit_log)
await using (var cdScope = app.Services.CreateAsyncScope())
{
    var cdInitializer = cdScope.ServiceProvider
        .GetRequiredService<HMS.Modules.Warehouse.Application.Services.CustomerDriverSchemaInitializer>();
    await cdInitializer.InitializeAsync();
}

// Step 5: Matching spatial indexes + proposal_source columns
await using (var matchScope = app.Services.CreateAsyncScope())
{
    var initializer = matchScope.ServiceProvider.GetRequiredService<IMatchingSpatialSchemaInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use exception middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("SignalRPolicy");
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Kích hoạt CORS
app.UseCors("SignalRPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map Endpoint tới Hub
app.MapHub<HmsFleetHub>("/hub/fleet");
app.MapTransportModule();

app.MapTraccarEndpoints();

app.Run();

