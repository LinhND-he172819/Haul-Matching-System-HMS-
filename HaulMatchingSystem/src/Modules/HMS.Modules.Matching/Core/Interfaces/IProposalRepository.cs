using HMS.Modules.Matching.Core.Models;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Repository for ShipmentProposal CRUD and queries.
    /// </summary>
    public interface IProposalRepository
    {
        // ── Queries ──
        Task<ShipmentProposal?> GetByIdAsync(Guid proposalId, CancellationToken ct);
        Task<ShipmentProposal?> GetPendingByShipmentAndTripPostAsync(Guid shipmentId, Guid tripPostId, CancellationToken ct);
        Task<List<ShipmentProposal>> GetPendingByTripPostAsync(Guid tripPostId, CancellationToken ct);
        Task<List<ShipmentProposal>> GetPendingByDriverAsync(Guid driverId, CancellationToken ct);
        Task<List<ShipmentProposal>> GetPendingByShipmentAsync(Guid shipmentId, CancellationToken ct);
        Task<bool> HasAcceptedProposalForShipmentAsync(Guid shipmentId, CancellationToken ct);
        Task<bool> HasPendingProposalForShipmentAndTripPostAsync(Guid shipmentId, Guid tripPostId, CancellationToken ct);

        // ── Trip queries ──
        Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct);
        Task<Trip?> GetTripByIdAsync(Guid tripId, CancellationToken ct);
        Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct);
        Task<TripPostRecord?> GetTripPostAsync(Guid tripPostId, CancellationToken ct);
        Task<Shipment?> GetShipmentAsync(Guid shipmentId, CancellationToken ct);

        // ── Mutations ──
        Task AddAsync(ShipmentProposal proposal, CancellationToken ct);
        Task UpdateAsync(ShipmentProposal proposal, CancellationToken ct);
        Task UpdateTripLoadAsync(Guid tripId, decimal addWeight, decimal addVolume, int expectedVersion, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);

        // ── Transaction ──
        Task BeginTransactionAsync(CancellationToken ct);
        Task CommitTransactionAsync(CancellationToken ct);
        Task RollbackTransactionAsync(CancellationToken ct);

        /// <summary>
        /// Returns the raw DbConnection for sharing with external services (e.g. ShipmentStateService).
        /// </summary>
        System.Data.Common.DbConnection? GetUnderlyingConnection();

        /// <summary>
        /// Returns the raw DbTransaction for sharing with external services.
        /// </summary>
        System.Data.Common.DbTransaction? GetUnderlyingTransaction();
    }

    /// <summary>
    /// Lightweight trip post record for proposal validation.
    /// </summary>
    public sealed class TripPostRecord
    {
        public Guid Id { get; set; }
        public Guid TripId { get; set; }
        public Guid CreatedBy { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset AcceptUntil { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PickupMode { get; set; } = string.Empty;
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
