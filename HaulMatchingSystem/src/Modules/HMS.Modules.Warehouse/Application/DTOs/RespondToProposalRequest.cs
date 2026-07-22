namespace HMS.Modules.Warehouse.Application.DTOs;

public sealed class RespondToProposalRequest
{
    /// <summary>Driver's rejection reason (required only for Reject action).</summary>
    public string? RejectReason { get; set; }
}

public sealed class ProposalResponse
{
    public Guid ProposalId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
