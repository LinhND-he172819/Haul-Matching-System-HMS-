using HMS.Modules.Matching.Application.DTOs;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Service for managing quotations.
    /// </summary>
    public interface IQuotationService
    {
        /// <summary>
        /// Create a new quotation draft for a proposal.
        /// </summary>
        Task<QuotationResponseDto> CreateQuotationAsync(
            Guid proposalId, CreateQuotationRequest request,
            Guid staffId, CancellationToken ct);

        /// <summary>
        /// Update an existing quotation draft.
        /// </summary>
        Task<QuotationResponseDto> UpdateQuotationAsync(
            Guid quotationId, UpdateQuotationRequest request,
            Guid staffId, CancellationToken ct);

        /// <summary>
        /// Send a quotation: Draft → Sent. Also transitions Shipment → PendingDeposit.
        /// </summary>
        Task SendQuotationAsync(
            Guid quotationId, Guid staffId, CancellationToken ct);

        /// <summary>
        /// Cancel a quotation: Draft/Sent → Cancelled.
        /// </summary>
        Task CancelQuotationAsync(
            Guid quotationId, Guid staffId, string? reason, CancellationToken ct);

        /// <summary>
        /// Get quotation details by ID.
        /// </summary>
        Task<QuotationResponseDto?> GetQuotationAsync(Guid quotationId, CancellationToken ct);

        /// <summary>
        /// Get quotation for a proposal.
        /// </summary>
        Task<QuotationSummaryDto?> GetActiveQuotationForProposalAsync(
            Guid proposalId, CancellationToken ct);

        /// <summary>
        /// Get quotation with shipment info for customer view.
        /// </summary>
        Task<QuotationSummaryDto?> GetQuotationForCustomerAsync(
            Guid quotationId, Guid customerId, CancellationToken ct);
    }
}
