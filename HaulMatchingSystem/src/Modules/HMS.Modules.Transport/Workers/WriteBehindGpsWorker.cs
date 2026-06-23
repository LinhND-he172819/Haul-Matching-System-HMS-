using HMS.Modules.Transport.Channels;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Data;
using HMS.Modules.Transport.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Workers
{
    public class WriteBehindGpsWorker : BackgroundService
    {
        private readonly GpsSyncChannel _gpsChannel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WriteBehindGpsWorker> _logger;

        public WriteBehindGpsWorker(GpsSyncChannel gpsChannel, IServiceProvider serviceProvider, ILogger<WriteBehindGpsWorker> logger)
        {
            _gpsChannel = gpsChannel;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Write-Behind GPS Worker khởi động.");

            // Vòng lặp chờ dữ liệu từ Channel
            await foreach (var batch in _gpsChannel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TransportDbContext>();

                    // Lọc các yêu cầu trùng lặp ngay trong batch, chống crash khi có nhiều yêu cầu cùng IdempotencyKey
                    var uniqueIncomingRequests = batch
                        .GroupBy(b => b.IdempotencyKey)
                        .Select(g => g.First())
                        .ToList();

                    var incomingKeys = uniqueIncomingRequests.Select(b => b.IdempotencyKey).ToList();

                    // Giải phóng RAM bằng sử dụng AsNoTracking và sử dụng ToHashSet để tra cứu nhanh các IdempotencyKey đã tồn tại
                    var existingKeysList = await dbContext.GpsLogs
                        .AsNoTracking()
                        .Where(g => incomingKeys.Contains(g.IdempotencyKey))
                        .Select(g => g.IdempotencyKey)
                        .ToListAsync(stoppingToken);

                    var existingKeys = existingKeysList.ToHashSet();
                    var logsToInsert = new List<GpsLog>();

                    foreach (var req in batch)
                    {
                        if (existingKeys.Contains(req.IdempotencyKey)) continue;

                        if (req.ActionType == OfflineActionType.GpsPing)
                        {
                            var tripId = req.Payload.GetProperty("tripId").GetGuid();
                            var lat = req.Payload.GetProperty("lat").GetDecimal();
                            var lng = req.Payload.GetProperty("lng").GetDecimal();

                            logsToInsert.Add(new GpsLog
                            {
                                Id = Guid.NewGuid(),
                                TripId = tripId,
                                Lat = lat,
                                Lng = lng,
                                DeviceTimestamp = req.DeviceTimestamp, // Bảo toàn thời gian gốc tại thiết bị
                                ServerReceivedAt = DateTime.UtcNow,    // Thời gian backfill thực tế
                                IdempotencyKey = req.IdempotencyKey
                            });
                        }
                        // Xử lý OfflineActionType.DeliveryConfirm ở đây nếu có
                    }

                    if (logsToInsert.Any())
                    {
                        await dbContext.GpsLogs.AddRangeAsync(logsToInsert, stoppingToken);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Đã điền lại {logsToInsert.Count} bản ghi GPS ngoại tuyến");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình ghi lùi (Write-Behind) GPS.");
                }
            }
        }
    }
}
