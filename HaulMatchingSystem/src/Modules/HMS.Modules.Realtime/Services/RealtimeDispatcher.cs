using HMS.Modules.Realtime.Hubs;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Realtime.Services
{
    public class RealtimeDispatcher : IRealtimeDispatcher
    {
        private readonly IHubContext<HmsFleetHub> _hubContext;
        private readonly ILogger<RealtimeDispatcher> _logger;
        private readonly ISmsSender _smsSender;

        public RealtimeDispatcher(
            IHubContext<HmsFleetHub> hubContext, 
            ILogger<RealtimeDispatcher> logger,
            ISmsSender smsSender)
        {
            _hubContext = hubContext;
            _logger = logger;
            _smsSender = smsSender;
        }

        public async Task SendSystemNotificationAsync(string message)
        {
            _logger.LogInformation($"[SignalR] THÔNG BÁO HỆ THỐNG: {message}");
            await _hubContext.Clients.All.SendAsync("ReceiveSystemMessage", message);
        }

        public async Task BroadcastVehicleLocationAsync(GpsPayload payload)
        {
            // Đẩy tọa độ lên Admin Dashboard
            await _hubContext.Clients.All.SendAsync("ReceiveGpsUpdate", payload);
        }

        public async Task SendAnomalyAlertAsync(AnomalyAlertPayload payload)
        {
            _logger.LogWarning($"[SignalR] CẢNH BÁO: {payload.AlertType} trên Chuyến {payload.TripId}");
            await _hubContext.Clients.Group("AdminGroup").SendAsync("ReceiveVehicleAlert", payload);
        }

        public async Task BroadcastShipmentStatusAsync(ShipmentStatusEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Shipment {payload.QrCode} chuyển sang trạng thái {payload.NewStatus}");

            // Có thể đẩy riêng cho 1 Customer cụ thể nếu hệ thống có tracking ConnectionId theo UserId
            // Hiện tại đẩy lên All để Admin Map cập nhật
            await _hubContext.Clients.All.SendAsync("ReceiveShipmentUpdate", payload);
        }

        public async Task BroadcastTripStatusAsync(TripStatusEventPayload payload)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveTripUpdate", payload);
        }

        public async Task BroadcastMatchingAcceptedAsync(object payload)
        {
            await _hubContext.Clients.Group("AdminGroup").SendAsync("MatchingAccepted", payload);
        }

        public async Task BroadcastMatchingRejectedAsync(object payload)
        {
            await _hubContext.Clients.Group("AdminGroup").SendAsync("MatchingRejected", payload);
        }

        public async Task BroadcastAdminStatsAsync(AdminStatsPayload stats)
        {
            await _hubContext.Clients.Group("AdminGroup").SendAsync("ReceiveAdminStats", stats);
        }

        public async Task SendDriverMatchingNotificationAsync(MatchingNotificationPayload payload)
        {
            // Bắn event tên là "ReceiveMatchingNotification" tới đúng 1 tài xế
            string targetGroup = $"Driver_{payload.DriverId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("ReceiveMatchingNotification", payload);
        }

        public async Task SendCustomerStatusNotificationAsync(CustomerStatusPayload payload)
        {
            _logger.LogInformation($"[Notification Router] Xử lý thông báo cho Khách hàng {payload.CustomerId} qua kênh: {payload.Preference}");
            var tasks = new List<Task>();

            // 1. Nhánh xử lý Web Push / In-App (SignalR)
            if (payload.Preference.HasFlag(NotificationChannel.Push))
            {
                // Lúc khách hàng login web/app
                string targetGroup = $"Customer_{payload.CustomerId}";
                var pushTask = _hubContext.Clients.Group(targetGroup).SendAsync("ReceiveShipmentStatusUpdate", payload);
                tasks.Add(pushTask);
            }

            // 2. Nhánh xử lý SMS
            if (payload.Preference.HasFlag(NotificationChannel.SMS) && !string.IsNullOrEmpty(payload.PhoneNumber))
            {
                // Gọi sang cổng SMS
                var smsTask = _smsSender.SendSmsAsync(payload.PhoneNumber, payload.Message);
                tasks.Add(smsTask);
            }

            // Chạy song song tất cả các kênh để tối ưu hiệu năng
            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }
    }
}
