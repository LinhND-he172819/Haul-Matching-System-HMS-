using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Transport.Core.Entities
{
    [Table("trip_exceptions", Schema = "transport")]
    public class TripException
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Column("exception_type")]
        public string ExceptionType { get; set; } = string.Empty;

        [Column("reason")]
        public string Reason { get; set; } = string.Empty;

        [Column("lat")]
        public decimal? Lat { get; set; }

        [Column("lng")]
        public decimal? Lng { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
