using HMS.Modules.Transport.API;
using HMS.Modules.Transport.Application.Services;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Modules.Transport.Infrastructure.Repositories;
using HMS.Modules.Transport.Infrastructure.Routing;
using HMS.Modules.Transport.Infrastructure.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HMS.Modules.Transport;

public static class TransportModule
{
    public static IServiceCollection AddTransportModule(this IServiceCollection services)
    {
        services.AddScoped<IHubRepository, PostgresHubRepository>();
        services.AddScoped<IHubService, HubService>();
        services.AddScoped<IVehicleRepository, PostgresVehicleRepository>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<ITripRepository, PostgresTripRepository>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<ITripRoutePlanner, OsrmTripRoutePlanner>();
        services.AddScoped<IHubLocationRepository, PostgresHubLocationRepository>();
        //services.AddSingleton<ITransportSchemaInitializer, PostgresTransportSchemaInitializer>();
        services.AddHttpClient<IOsrmRouteClient, OsrmRouteClient>((serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = configuration.GetValue<string>("Osrm:BaseUrl") ?? "https://router.project-osrm.org";
            var timeoutSeconds = configuration.GetValue("Osrm:TimeoutSeconds", 10);

            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        return services;
    }

    public static async Task<WebApplication> InitializeTransportModuleAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        //await using var scope = app.Services.CreateAsyncScope();
        //var initializer = scope.ServiceProvider.GetRequiredService<ITransportSchemaInitializer>();

        //await initializer.InitializeAsync(cancellationToken);

        return app;
    }

    public static IEndpointRouteBuilder MapTransportModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHubEndpoints();
        endpoints.MapVehicleEndpoints();
        endpoints.MapTripEndpoints();

        return endpoints;
    }
}
