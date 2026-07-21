using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Data;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Events;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Application.EventHandlers
{
    public class SyncGpsFromTelemetryHandler : INotificationHandler<GpsPingReceivedEvent>
    {
        private readonly TransportDbContext _dbContext;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<SyncGpsFromTelemetryHandler> _logger;

        public SyncGpsFromTelemetryHandler(
            TransportDbContext dbContext,
            IRealtimeDispatcher dispatcher,
            ILogger<SyncGpsFromTelemetryHandler> logger)
        {
            _dbContext = dbContext;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task Handle(GpsPingReceivedEvent notification, CancellationToken cancellationToken)
        {
            Console.WriteLine($"\n[HANDLER] 🏃 Bắt đầu xử lý GPS cho thiết bị: {notification.DeviceId}");

            if (!Guid.TryParse(notification.DeviceId, out var deviceGuid))
            {
                _logger.LogWarning($"[TELEMETRY] Lỗi định dạng DeviceId. Đang chờ Guid nhưng nhận được: '{notification.DeviceId}'");
                return;
            }

            // Tìm chuyến đi Active ứng với VehicleId này
            var activeTripId = await _dbContext.Trips
                .Where(t => t.Status == TripStatus.Active && t.VehicleId == deviceGuid)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeTripId == null)
            {
                Console.WriteLine($"[HANDLER] ⚠️ BỎ QUA: Xe có ID '{deviceGuid}' hiện KHÔNG CÓ chuyến đi Active nào trong DB.");
                return;
            }

            // Ghi nhận tọa độ
            var gpsLog = new GpsLog
            {
                Id = Guid.NewGuid(),
                TripId = activeTripId.Value,
                Lat = (decimal)notification.Lat,
                Lng = (decimal)notification.Lng,
                DeviceTimestamp = DateTimeOffset.FromUnixTimeSeconds(notification.Timestamp 
                    ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime,
                ServerReceivedAt = DateTime.UtcNow,
                IdempotencyKey = $"telemetry-{notification.DeviceId}-{notification.ServerReceivedAt.Ticks}"
            };

            _dbContext.GpsLogs.Add(gpsLog);


            string signalLossStr = ExceptionType.Signal_Loss.ToString();
            var activeExceptions = await _dbContext.TripExceptions
                .Where(e => e.TripId == activeTripId.Value && e.ExceptionType == signalLossStr)
                .ToListAsync(cancellationToken);

            if (activeExceptions.Any())
            {
                _dbContext.TripExceptions.RemoveRange(activeExceptions);
                Console.WriteLine($"[HANDLER] 🚑 Xe {notification.DeviceId} đã CÓ SÓNG. Đang xóa {activeExceptions.Count} cảnh báo lỗi.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            Console.WriteLine($"[HANDLER] ✅ ĐÃ LƯU TỌA ĐỘ VÀO POSTGRESQL THÀNH CÔNG (TripId: {activeTripId.Value})");

            // Bắn SignalR
            await _dispatcher.BroadcastVehicleLocationAsync(new GpsPayload
            {
                TripId = activeTripId.Value,
                Lat = notification.Lat,
                Lng = notification.Lng,
                DeviceTimestamp = gpsLog.DeviceTimestamp
            });
        }
    }
}