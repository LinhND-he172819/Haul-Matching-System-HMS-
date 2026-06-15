namespace HMS.Modules.Matching.Application.Requests
{
    public class AcceptSelectedRequest
    {
        public List<Guid> ShipmentIds { get; set; } = new();
    }
}
