using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HMS.Modules.Transport.API;

public static class HubEndpoints
{
    public static IEndpointRouteBuilder MapHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/hubs")
            .WithTags("Hubs");

        group.MapGet("/", async (
            string? search,
            IHubService service,
            CancellationToken cancellationToken) =>
        {
            var hubs = await service.ListAsync(search, cancellationToken);

            return Results.Ok(hubs);
        });

        group.MapGet("/{id:guid}", async (Guid id, IHubService service, CancellationToken cancellationToken) =>
        {
            var hub = await service.GetByIdAsync(id, cancellationToken);

            return hub is null ? Results.NotFound() : Results.Ok(hub);
        });

        group.MapPost("/", async (
            CreateHubRequest request,
            IHubService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var hub = await service.CreateAsync(request, cancellationToken);

                return Results.Created($"/api/hubs/{hub.Id}", hub);
            }
            catch (ArgumentException exception)
            {
                return ValidationResult(exception);
            }
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateHubRequest request,
            IHubService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var hub = await service.UpdateAsync(id, request, cancellationToken);

                return hub is null ? Results.NotFound() : Results.Ok(hub);
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
        var key = string.IsNullOrWhiteSpace(exception.ParamName) ? "hub" : exception.ParamName;

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [exception.Message]
        });
    }
}
