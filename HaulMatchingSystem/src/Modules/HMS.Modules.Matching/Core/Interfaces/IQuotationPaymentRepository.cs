using HMS.Modules.Matching.Core.Models;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Repository for Quotation and Payment operations.
    /// </summary>
    public interface IQuotationPaymentRepository
    {
        // ── Quotation ──
        Task<Quotation?> GetQuotationByIdAsync(Guid quotationId, CancellationToken ct);
        Task<List<Quotation>> GetQuotationsByProposalIdAsync(Guid proposalId, CancellationToken ct);
        Task<Quotation?> GetActiveDraftOrSentQuotationAsync(Guid proposalId, CancellationToken ct);
        Task AddQuotationAsync(Quotation quotation, CancellationToken ct);
        Task UpdateQuotationAsync(Quotation quotation, CancellationToken ct);

        // ── Payment ──
        Task<Payment?> GetPaymentByIdAsync(Guid paymentId, CancellationToken ct);
        Task<Payment?> GetPaymentByTransactionRefAsync(string transactionReference, CancellationToken ct);
        Task<Payment?> GetPaymentByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
        Task<Payment?> GetSuccessfulDepositForQuotationAsync(Guid quotationId, CancellationToken ct);
        Task<List<Payment>> GetPaymentsByQuotationIdAsync(Guid quotationId, CancellationToken ct);
        Task<List<Payment>> GetPaymentsByShipmentIdAsync(Guid shipmentId, CancellationToken ct);
        Task AddPaymentAsync(Payment payment, CancellationToken ct);
        Task UpdatePaymentAsync(Payment payment, CancellationToken ct);

        // ── Combined ──
        Task<Quotation?> GetQuotationWithProposalAsync(Guid quotationId, CancellationToken ct);
        Task<ShipmentProposal?> GetProposalByIdAsync(Guid proposalId, CancellationToken ct);
        Task<Shipment?> GetShipmentByIdAsync(Guid shipmentId, CancellationToken ct);
        Task<Vehicle?> GetVehicleByIdAsync(Guid vehicleId, CancellationToken ct);
        Task<Trip?> GetTripByIdAsync(Guid tripId, CancellationToken ct);
        Task<Guid?> GetTripPostIdForProposalAsync(Guid proposalId, CancellationToken ct);
        Task<int> SaveChangesAsync(CancellationToken ct);

        // ── Capacity ──
        Task<bool> HasSufficientCapacityAsync(Guid tripId, decimal weightKg, decimal volumeCbm, CancellationToken ct);
        Task UpdateTripCapacityAsync(Guid tripId, decimal weightDelta, decimal volumeDelta, CancellationToken ct);
        Task LinkShipmentToTripAsync(Guid tripId, Guid shipmentId, CancellationToken ct);

        // ── Audit ──
        Task InsertAuditLogAsync(string entityType, Guid entityId, string action, string? details, Guid? performedBy, CancellationToken ct);

        // ── Notifications ──
        Task SaveNotificationAsync(Guid userId, string title, string message, string? entityType, Guid? entityId, CancellationToken ct);
    }
}
