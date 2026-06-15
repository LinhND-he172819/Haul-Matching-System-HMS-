using HMS.Modules.Transport.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMS.Modules.Transport.DTOs
{
    public class OfflineSyncRequest
    {
        public string IdempotencyKey { get; set; } = string.Empty; // Mã UUID từ thiết bị
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OfflineActionType ActionType { get; set; }
        public JsonElement Payload { get; set; } // Dữ liệu động
        public DateTime DeviceTimestamp { get; set; }
    }
}
