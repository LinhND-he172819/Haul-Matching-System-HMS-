using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Requests;

namespace HMS.Modules.Matching.Core.Interfaces
{
    public interface IMatchingService
    {
        Task<MatchingSuggestionsResponse?> GetSuggestionsForDriverAsync(Guid driverId, CancellationToken ct);

        Task AcceptAllAsync(Guid driverId, CancellationToken ct);

        Task RejectAllAsync(Guid driverId, CancellationToken ct);

        Task AcceptSelectedAsync(Guid driverId, AcceptSelectedRequest request, CancellationToken ct);

        Task RejectSelectedAsync(Guid driverId, RejectSelectedRequest request, CancellationToken ct);
    }
}
