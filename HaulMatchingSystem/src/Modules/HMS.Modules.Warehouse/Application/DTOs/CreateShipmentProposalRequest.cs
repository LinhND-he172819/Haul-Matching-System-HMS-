namespace HMS.Modules.Warehouse.Application.DTOs;

public sealed class CreateShipmentProposalRequest
{
    public Guid TripPostId { get; set; }
    public string? Message { get; set; }
}
