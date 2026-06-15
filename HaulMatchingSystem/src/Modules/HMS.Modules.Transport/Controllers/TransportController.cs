using HMS.Modules.Transport.Data;
using HMS.Modules.Transport.DTOs;
using HMS.Modules.Transport.Entities;
using HMS.Modules.Transport.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Controllers
{
    [ApiController]
    [Route("api/transport")]
    public class TransportController : ControllerBase
    {
        private readonly TransportDbContext _context;
        private readonly ILogger<TransportController> _logger;

        public TransportController(TransportDbContext context, ILogger<TransportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("sync-offline")]
        public async Task<IActionResult> SyncOfflineData([FromBody] List<OfflineSyncRequest> requests)
        {
            if (requests == null || !requests.Any())
                return BadRequest("No data to sync.");

            int successCount = 0;
            int ignoredCount = 0;

            // sánh nhanh idem key trong db với incoming request để loại bỏ những request đã tồn tại
            var incomingKeys = requests.Select(r => r.IdempotencyKey).ToList();
            var existingKeys = await _context.GpsLogs
                .Where(g => incomingKeys.Contains(g.IdempotencyKey))
                .Select(g => g.IdempotencyKey)
                .ToListAsync();

            foreach (var req in requests)
            {
                if(existingKeys.Contains(req.IdempotencyKey))
                {
                    _logger.LogInformation($"[Sync] Ignored duplicate action: {req.IdempotencyKey}");
                    ignoredCount++;
                    continue; // bỏ qua request đã tồn tại
                }
                
                switch(req.ActionType)
                {
                    case OfflineActionType.GpsPing:
                        var tripId = req.Payload.GetProperty("tripId").GetGuid();
                        var lat = req.Payload.GetProperty("lat").GetDecimal();
                        var lng = req.Payload.GetProperty("lng").GetDecimal();

                        var newLog = new GpsLog
                        {
                            Id = Guid.NewGuid(),
                            TripId = tripId,
                            Lat = lat,
                            Lng = lng,
                            DeviceTimestamp = req.DeviceTimestamp,
                            ServerReceivedAt = DateTime.UtcNow,
                            IdempotencyKey = req.IdempotencyKey
                        };

                        _context.GpsLogs.Add(newLog);
                        successCount++;
                        break;
                    case OfflineActionType.DeliveryConfirm:
                        // xử lý giao hàng
                        break;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = "Offline sync completed.",
                Processed = successCount,
                IgnoredDuplicates = ignoredCount
            });
        }
    }
}
