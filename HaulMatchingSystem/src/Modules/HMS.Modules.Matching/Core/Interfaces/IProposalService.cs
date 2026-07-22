using HMS.Modules.Matching.Application.DTOs;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Service for managing shipment proposals in the new Trip Post flow.
    /// </summary>
    public interface IProposalService
    {
        // ── Customer APIs ──
        Task<CreateProposalResponse> CreateProposalAsync(
            Guid tripPostId, Guid customerId, CreateProposalRequest request, CancellationToken ct);

        Task CancelProposalAsync(Guid proposalId, Guid customerId, CancellationToken ct);

        // ── Driver APIs ──
        Task<DriverProposalsResponse?> GetDriverPendingProposalsAsync(Guid driverId, CancellationToken ct);

        Task<ProposalDto> AcceptProposalAsync(Guid proposalId, Guid driverId, CancellationToken ct);

        Task<ProposalDto> RejectProposalAsync(Guid proposalId, Guid driverId, RejectProposalRequest request, CancellationToken ct);

        Task<TripCapacityDto> AcceptAllProposalsAsync(Guid driverId, AcceptAllProposalsRequest request, CancellationToken ct);
    }
}
