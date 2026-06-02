using HMS.Modules.Realtime.Interfaces;
using HMS.Modules.Realtime.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Realtime.Workers
{
    public class DashboardStatsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DashboardStatsWorker> _logger;

        public DashboardStatsWorker(IServiceProvider serviceProvider, ILogger<DashboardStatsWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Admin Dashboard Stats Worker khởi động.");
            
            // loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo Scope mới để gọi DbContext vì BackgroundService là Singleton
                    using var scope = _serviceProvider.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IRealtimeDispatcher>();

                    // Gọi vào Database (TransportDbContext & WarehouseDbContext) để đếm số liệu thực tế
                    // hiện tại tạo số liệu Mock để test luồng SignalR chạy tốt trước.
                    var mockStats = new AdminStatsPayload
                    {
                        ActiveTripCount = new Random().Next(5, 20),
                        InTransitShipments = new Random().Next(100, 500),
                        AvgVehicleUtilisation = Math.Round(new Random().NextDouble() * 100, 2), // 0% - 100%
                        HubItemsWaitingOver3Days = new Random().Next(0, 10),
                        LastUpdated = DateTime.UtcNow
                    };

                    // Broadcast số liệu xuống cho Admin
                    await dispatcher.BroadcastAdminStatsAsync(mockStats);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi phát sóng số liệu admin.");
                }

                // Nghỉ 10 giây rồi tính toán và bắn lại (Chu kỳ cập nhật Real-time)
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
