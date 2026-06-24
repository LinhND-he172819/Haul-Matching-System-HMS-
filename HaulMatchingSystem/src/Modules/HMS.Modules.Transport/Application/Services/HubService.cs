using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;

namespace HMS.Modules.Transport.Application.Services;

public sealed class HubService(IHubRepository repository) : IHubService
{
    public async Task<HubResponse> CreateAsync(CreateHubRequest request, CancellationToken cancellationToken = default)
    {
        var hub = Hub.Create(request.Name, request.Address, request.Latitude, request.Longitude);

        await repository.AddAsync(hub, cancellationToken);

        return ToResponse(hub);
    }

    public async Task<IReadOnlyCollection<HubResponse>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var hubs = await repository.ListAsync(search, cancellationToken);

        return hubs
            .OrderBy(hub => hub.Name)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<HubResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hub = await repository.GetByIdAsync(id, cancellationToken);

        return hub is null ? null : ToResponse(hub);
    }

    public async Task<HubResponse?> UpdateAsync(
        Guid id,
        UpdateHubRequest request,
        CancellationToken cancellationToken = default)
    {
        var hub = await repository.GetByIdAsync(id, cancellationToken);
        if (hub is null)
        {
            return null;
        }

        hub.Update(request.Name, request.Address, request.Latitude, request.Longitude);

        await repository.UpdateAsync(hub, cancellationToken);

        return ToResponse(hub);
    }

    private static HubResponse ToResponse(Hub hub)
    {
        return new HubResponse(
            hub.Id,
            hub.Name,
            hub.Address,
            hub.Latitude,
            hub.Longitude,
            hub.CreatedAt,
            hub.UpdatedAt);
    }
}
