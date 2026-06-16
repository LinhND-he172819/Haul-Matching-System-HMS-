using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Application.Services;
using HMS.Shared.Core.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HMS.Modules.Transport.API;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/trips")
            .WithTags("Trips");

        group.MapPost("/", async (CreateTripRequest request, ITripService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var trip = await service.CreateAsync(request, cancellationToken);

                return Results.Created($"/api/trips/{trip.Id}", trip);
            }
            catch (RoutePlanningException exception)
            {
                return RoutePlanningResult(exception);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
        });

        group.MapGet("/", async (
            Guid? driverId,
            TripStatus? status,
            ITripService service,
            CancellationToken cancellationToken) =>
        {
            var trips = await service.ListAsync(driverId, status, cancellationToken);

            return Results.Ok(trips);
        });

        group.MapGet("/{id:guid}", async (Guid id, ITripService service, CancellationToken cancellationToken) =>
        {
            var trip = await service.GetByIdAsync(id, cancellationToken);

            return trip is null ? Results.NotFound() : Results.Ok(trip);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTripRequest request,
            ITripService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var trip = await service.UpdateAsync(id, request, cancellationToken);

                return trip is null ? Results.NotFound() : Results.Ok(trip);
            }
            catch (RoutePlanningException exception)
            {
                return RoutePlanningResult(exception);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { message = exception.Message });
            }
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            ChangeTripStatusRequest request,
            ITripService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var trip = await service.ChangeStatusAsync(id, request, cancellationToken);

                return trip is null ? Results.NotFound() : Results.Ok(trip);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { message = exception.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITripService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(id, cancellationToken);

            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }

    private static IResult ValidationResult(ArgumentException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.ParamName) ? "trip" : exception.ParamName;

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [exception.Message]
        });
    }

    private static IResult RoutePlanningResult(RoutePlanningException exception)
    {
        return Results.Problem(
            title: "Route planning failed",
            detail: exception.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
}
