using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HMS.Modules.Transport.API;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/vehicles")
            .WithTags("Vehicles");

        group.MapGet("/", async (
            string? search,
            string? status,
            IVehicleService service,
            CancellationToken cancellationToken) =>
        {
            var vehicles = await service.ListAsync(search, status, cancellationToken);

            return Results.Ok(vehicles);
        });

        group.MapGet("/{id:guid}", async (Guid id, IVehicleService service, CancellationToken cancellationToken) =>
        {
            var vehicle = await service.GetByIdAsync(id, cancellationToken);

            return vehicle is null ? Results.NotFound() : Results.Ok(vehicle);
        });

        group.MapPost("/", async (
            CreateVehicleRequest request,
            IVehicleService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var vehicle = await service.CreateAsync(request, cancellationToken);

                return Results.Created($"/api/vehicles/{vehicle.Id}", vehicle);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateVehicleRequest request,
            IVehicleService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var vehicle = await service.UpdateAsync(id, request, cancellationToken);

                return vehicle is null ? Results.NotFound() : Results.Ok(vehicle);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
        });

        return endpoints;
    }

    private static IResult ValidationResult(ArgumentException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.ParamName) ? "vehicle" : exception.ParamName;

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [exception.Message]
        });
    }
}
