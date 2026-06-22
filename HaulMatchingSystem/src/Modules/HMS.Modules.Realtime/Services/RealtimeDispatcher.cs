using HMS.Modules.Realtime.Hubs;
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

        public RealtimeDispatcher(IHubContext<HmsFleetHub> hubContext, ILogger<RealtimeDispatcher> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
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
    }
}
