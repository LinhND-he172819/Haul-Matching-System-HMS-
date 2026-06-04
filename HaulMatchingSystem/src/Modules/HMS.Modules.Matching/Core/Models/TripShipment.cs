using System.ComponentModel.DataAnnotations;

namespace HMS.Modules.Matching.Core.Models
{
    public class TripShipment
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TripId { get; set; }

        public Guid ShipmentId { get; set; }

        public int DeliverySequence { get; set; }

        public string? Status { get; set; }

        public DateTime? SuggestedAt { get; set; }

        public DateTime? RespondedAt { get; set; }

        public Guid? RespondedBy { get; set; }
    }
}
