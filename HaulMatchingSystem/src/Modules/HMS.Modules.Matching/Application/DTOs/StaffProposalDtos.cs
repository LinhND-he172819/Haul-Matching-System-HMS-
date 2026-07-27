namespace HMS.Modules.Matching.Application.DTOs
{
    /// <summary>
    /// Summary DTO for proposal listing (Staff / Admin view).
    /// </summary>
    public class StaffProposalSummaryDto
    {
        public Guid ProposalId { get; set; }
        public string? ProposalCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Shipment info
        public string? ShipmentCode { get; set; }
        public string? Commodity { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeCbm { get; set; }

        // Shipment
        public Guid ShipmentId { get; set; }

        // Sender / Pickup
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;

        // Receiver / Delivery
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? DeliveryAddress { get; set; }

        // Trip info
        public Guid TripPostId { get; set; }
        public Guid TripId { get; set; }
        public string? TripCode { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }

        // Trip capacity
        public decimal RemainingWeight { get; set; }
        public decimal RemainingVolume { get; set; }

        // Customer info
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
    }

    /// <summary>
    /// Detailed proposal view for Staff / Admin.
    /// </summary>
    public class StaffProposalDetailDto
    {
        public Guid ProposalId { get; set; }
        public string? ProposalCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectReason { get; set; }

        // Shipment
        public ShipmentInfoDto Shipment { get; set; } = new();

        // Sender / Pickup (from proposal)
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public double? PickupLatitude { get; set; }
        public double? PickupLongitude { get; set; }
        public string? PickupNote { get; set; }

        // Trip info
        public TripInfoDto Trip { get; set; } = new();

        // Trip capacity
        public TripCapacityInfoDto TripCapacity { get; set; } = new();

        // Customer
        public CustomerInfoDto Customer { get; set; } = new();

        // Quotation history
        public List<QuotationSummaryDto> QuotationHistory { get; set; } = new();

        // Current active quotation
        public QuotationSummaryDto? CurrentQuotation { get; set; }

        // Audit timeline
        public List<ProposalAuditEntry> AuditTimeline { get; set; } = new();
    }

    public class ShipmentInfoDto
    {
        public Guid Id { get; set; }
        public string? ShipmentCode { get; set; }
        public string? Commodity { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeCbm { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? SpecialHandlingNote { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TripInfoDto
    {
        public Guid TripPostId { get; set; }
        public Guid TripId { get; set; }
        public string? TripCode { get; set; }
        public string? Title { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public DateTime? AcceptUntil { get; set; }
        public string? PickupMode { get; set; }
        public decimal MaxWeight { get; set; }
        public decimal MaxVolume { get; set; }
    }

    public class TripCapacityInfoDto
    {
        public decimal CurrentWeight { get; set; }
        public decimal CurrentVolume { get; set; }
        public decimal MaxWeight { get; set; }
        public decimal MaxVolume { get; set; }
        public decimal RemainingWeight { get; set; }
        public decimal RemainingVolume { get; set; }
    }

    public class CustomerInfoDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class QuotationSummaryDto
    {
        public Guid Id { get; set; }
        public string? QuotationCode { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public DateTime? SentAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProposalAuditEntry
    {
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public Guid? PerformedBy { get; set; }
        public string? PerformedByName { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Paged list result.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// List item DTO for quotation management (Staff / Admin view).
    /// </summary>
    public class StaffQuotationListItem
    {
        public Guid Id { get; set; }
        public string? QuotationCode { get; set; }
        public Guid ProposalId { get; set; }
        public string? ProposalCode { get; set; }
        public string? ShipmentCode { get; set; }
        public string? CustomerName { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DepositAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public DateTime? SentAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// List item DTO for payment monitoring (Staff / Admin view).
    /// </summary>
    public class StaffPaymentListItem
    {
        public Guid Id { get; set; }
        public string? PaymentCode { get; set; }
        public Guid QuotationId { get; set; }
        public string? QuotationCode { get; set; }
        public Guid ShipmentId { get; set; }
        public string? ShipmentCode { get; set; }
        public string? CustomerName { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Request to approve a proposal.
    /// </summary>
    public class ApproveProposalRequest { }

    /// <summary>
    /// Request to reject a proposal.
    /// </summary>
    public class RejectProposalRequestByStaff
    {
        public string Reason { get; set; } = string.Empty;
    }
}
