using HMS.Modules.Transport.Core.Entities;

namespace HMS.Modules.Transport.Core.Interfaces;

public interface IHubRepository
{
    Task AddAsync(Hub hub, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Hub>> ListAsync(string? search, CancellationToken cancellationToken = default);
    Task<Hub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Hub hub, CancellationToken cancellationToken = default);
}
