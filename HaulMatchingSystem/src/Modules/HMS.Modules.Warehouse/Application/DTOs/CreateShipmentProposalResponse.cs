namespace HMS.Modules.Warehouse.Application.DTOs;

public sealed class CreateShipmentProposalResponse
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid TripPostId { get; set; }
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
}
