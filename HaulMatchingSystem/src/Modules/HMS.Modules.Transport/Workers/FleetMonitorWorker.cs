using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Data;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Workers
{
    public class FleetMonitorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FleetMonitorWorker> _logger;

        public FleetMonitorWorker(IServiceProvider serviceProvider, ILogger<FleetMonitorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Fleet Monitor Worker (Signal Loss Detector) khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo scope vì BackgroundService là Singleton, còn DbContext là Scoped
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TransportDbContext>();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IRealtimeDispatcher>();

                    // Ngưỡng thời gian: mất tín hiệu nếu không có ping trong 3 phút qua
                    var thresholdTime = DateTime.UtcNow.AddMinutes(-3);

                    // 1. Tìm các chuyến xe đang chạy và lấy thông tin GPS cuối cùng
                    var staleTrips = await dbContext.Trips
                        .Where(t => t.Status == TripStatus.Active)
                        .Select(t => new
                        {
                            TripId = t.Id,
                            LastGps = dbContext.GpsLogs
                                        .Where(g => g.TripId == t.Id)
                                        .OrderByDescending(g => g.ServerReceivedAt)
                                        .Select(g => new { g.Lat, g.Lng, g.ServerReceivedAt })
                                        .FirstOrDefault()
                        })
                        .Where(x => x.LastGps == null || x.LastGps.ServerReceivedAt < thresholdTime)
                        .ToListAsync(stoppingToken);

                    foreach (var trip in staleTrips)
                    {
                        string signalLossStr = ExceptionType.Signal_Loss.ToString();
                        // Kiểm tra xem đã ghi nhận lỗi này cho chuyến này chưa (tránh spam DB)
                        var alreadyAlerted = await dbContext.TripExceptions
                            .AnyAsync(e => e.TripId == trip.TripId && e.ExceptionType == signalLossStr, stoppingToken);

                        if (!alreadyAlerted)
                        {
                            _logger.LogWarning($"⚠️ Phát hiện mất tín hiệu: Trip {trip.TripId}");

                            // 2. Ghi nhận nghiệp vụ vào bảng transport.trip_exceptions
                            var newException = new TripException
                            {
                                Id = Guid.NewGuid(),
                                TripId = trip.TripId,
                                ExceptionType = ExceptionType.Signal_Loss.ToString(), // Lưu dạng chuỗi vào DB
                                Reason = "No GPS ping received for over 3 minutes.",
                                CreatedAt = DateTime.UtcNow,
                                Lat = trip.LastGps != null ? trip.LastGps.Lat : null,
                                Lng = trip.LastGps != null ? trip.LastGps.Lng : null
                            };
                            dbContext.TripExceptions.Add(newException);

                            // 3. Khởi tạo Payload theo đúng chuẩn Model đã định nghĩa
                            var alertPayload = new AnomalyAlertPayload
                            {
                                TripId = trip.TripId,
                                AlertType = ExceptionType.Signal_Loss,
                                Message = "Mất tín hiệu GPS quá 3 phút. Đang chờ đồng bộ thiết bị.",
                                Lat = trip.LastGps != null ? (double)trip.LastGps.Lat : 0,
                                Lng = trip.LastGps != null ? (double)trip.LastGps.Lng : 0,
                                DetectedAt = DateTime.UtcNow
                            };

                            // 4. Nhờ Dispatcher của module Realtime phát sóng lên UI
                            await dispatcher.SendAnomalyAlertAsync(alertPayload);
                        }
                    }

                    // Lưu tất cả record ngoại lệ xuống Database trong 1 transaction duy nhất
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét tín hiệu GPS định kỳ.");
                }

                // Chờ 30 giây rồi thực hiện vòng quét tiếp theo
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
