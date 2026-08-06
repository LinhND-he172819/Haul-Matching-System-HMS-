using HMS.Modules.Matching.Application.DTOs;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Service for Warehouse Staff / Admin proposal management.
    /// </summary>
    public interface IStaffProposalService
    {
        /// <summary>
        /// List proposals filtered by status. Staff sees only their hub's proposals.
        /// Admin sees all. Supports filtering by proposalSource and driverId.
        /// </summary>
        Task<PagedResult<StaffProposalSummaryDto>> GetProposalsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, string? proposalSource, Guid? driverId,
            int page, int pageSize,
            CancellationToken ct);

        /// <summary>
        /// Get detailed proposal information.
        /// </summary>
        Task<StaffProposalDetailDto?> GetProposalDetailAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct);

        /// <summary>
        /// Approve a proposal: PendingReview → Approved.
        /// Does NOT change shipment status or deduct capacity.
        /// </summary>
        Task ApproveProposalAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            CancellationToken ct);

        /// <summary>
        /// Reject a proposal: PendingReview → Rejected.
        /// Also transitions Shipment → Cancelled.
        /// </summary>
        Task RejectProposalAsync(
            Guid proposalId, Guid staffId, string? role, Guid? hubId,
            string reason, CancellationToken ct);

        /// <summary>
        /// List quotations with optional status filter and pagination.
        /// Warehouse_Staff sees only quotations for their hub's proposals.
        /// Admin sees all.
        /// </summary>
        Task<PagedResult<StaffQuotationListItem>> GetQuotationsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, int page, int pageSize,
            CancellationToken ct);

        /// <summary>
        /// List payments with optional status filter and pagination.
        /// Warehouse_Staff sees only payments for their hub.
        /// Admin sees all.
        /// </summary>
        Task<PagedResult<StaffPaymentListItem>> GetPaymentsAsync(
            Guid staffId, string? role, Guid? hubId,
            string? status, int page, int pageSize,
            CancellationToken ct);
    }
}
