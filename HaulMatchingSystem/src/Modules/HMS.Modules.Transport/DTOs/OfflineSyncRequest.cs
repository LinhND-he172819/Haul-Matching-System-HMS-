using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HMS.Modules.Transport.DTOs
{
    public class OfflineSyncRequest
    {
        public string IdempotencyKey { get; set; } = string.Empty; // Mã UUID từ thiết bị
        public string ActionType { get; set; } = string.Empty; // "GPS_PING" hoặc "DELIVERY_CONFIRM"
        public JsonElement Payload { get; set; } // Dữ liệu động
        public DateTime DeviceTimestamp { get; set; }
    }
}
