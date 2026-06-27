using HMS.Modules.Transport.Application.DTOs;

namespace HMS.Modules.Transport.Application.Services;

public interface IHubService
{
    Task<HubResponse> CreateAsync(CreateHubRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<HubResponse>> ListAsync(string? search, CancellationToken cancellationToken = default);
    Task<HubResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HubResponse?> UpdateAsync(Guid id, UpdateHubRequest request, CancellationToken cancellationToken = default);
}
