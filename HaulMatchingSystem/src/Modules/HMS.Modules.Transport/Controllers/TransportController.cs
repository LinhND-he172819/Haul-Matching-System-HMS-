using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Channels;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Modules.Transport.Controllers
{
    [ApiController]
    [Route("api/transport")]
    public class TransportController : ControllerBase
    {
        private readonly GpsSyncChannel _gpsChannel;
        public TransportController(GpsSyncChannel gpsChannel)
        {
            _gpsChannel = gpsChannel;
        }

        [HttpPost("sync-offline")]
        public async Task<IActionResult> SyncOfflineData([FromBody] List<OfflineSyncRequest> requests)
        {
            if (requests == null || !requests.Any())
                return BadRequest("Không có data để đồng bộ");

            // Đẩy vào hàng đợi bộ nhớ RAM
            await _gpsChannel.AddSyncBatchAsync(requests);
            // Trả về 202 Accepted (Đã tiếp nhận nhưng chưa xử lý xong)
            return Accepted(new 
            { Message = "Dữ liệu đồng bộ đã được chấp nhận và xếp vào hàng chờ xử lý",
              ReceivedCount = requests.Count
            });
        }
    }
}
