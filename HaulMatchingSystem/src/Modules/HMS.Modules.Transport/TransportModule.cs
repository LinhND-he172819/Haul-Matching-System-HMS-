using HMS.Modules.Transport.API;
using HMS.Modules.Transport.Application.Services;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Modules.Transport.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HMS.Modules.Transport;

public static class TransportModule
{
    public static IServiceCollection AddTransportModule(this IServiceCollection services)
    {
        services.AddScoped<ITripRepository, PostgresTripRepository>();
        services.AddScoped<ITripService, TripService>();

        return services;
    }

    public static IEndpointRouteBuilder MapTransportModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTripEndpoints();

        return endpoints;
    }
}
