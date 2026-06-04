namespace HMS.Modules.Matching.Application.Requests
{
    public class RejectSelectedRequest
    {
        public List<Guid> ShipmentIds { get; set; } = new();
    }
}
