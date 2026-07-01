using MediatR;

namespace HMS.Shared.Core.Events
{
    public class GpsPingReceivedEvent : INotification
    {
        public string DeviceId { get; set; } = string.Empty;
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public long? Timestamp { get; set; }
        public DateTime ServerReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
