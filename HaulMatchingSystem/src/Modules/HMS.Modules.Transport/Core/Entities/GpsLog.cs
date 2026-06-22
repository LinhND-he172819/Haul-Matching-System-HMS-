using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS.Modules.Transport.Core.Entities
{
    [Table("gps_logs", Schema = "transport")]
    public class GpsLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Column("lat")]
        public decimal Lat { get; set; }

        [Column("lng")]
        public decimal Lng { get; set; }

        [Column("speed")]
        public decimal? Speed { get; set; }

        [Column("device_timestamp")]
        public DateTime DeviceTimestamp { get; set; }

        [Column("server_received_at")]
        public DateTime ServerReceivedAt { get; set; }

        // Khóa chống ghi đè
        [Column("idempotency_key")]
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
