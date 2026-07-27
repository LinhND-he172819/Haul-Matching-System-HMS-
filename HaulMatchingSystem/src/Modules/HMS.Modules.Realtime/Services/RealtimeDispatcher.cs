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

        // ── Tầng 7: Shipment Proposal Events ──

        public async Task SendNewProposalToDriverAsync(Guid driverId, ProposalEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] New proposal for Driver {driverId}: TripPost {payload.TripPostId}, {payload.PendingProposalCount} pending");
            string targetGroup = $"Driver_{driverId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("NewShipmentProposal", payload);
        }

        public async Task SendProposalCancelledToDriverAsync(Guid driverId, ProposalEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Proposal cancelled for Driver {driverId}: Proposal {payload.ProposalId}");
            string targetGroup = $"Driver_{driverId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("ShipmentProposalCancelled", payload);
        }

        public async Task SendTripCapacityUpdatedToDriverAsync(Guid driverId, ProposalEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Trip capacity updated for Driver {driverId}: {payload.RemainingWeightKg}kg / {payload.RemainingVolumeCbm}m³ remaining");
            string targetGroup = $"Driver_{driverId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("TripCapacityUpdated", payload);
        }

        public async Task SendProposalStatusToCustomerAsync(Guid customerId, ProposalEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Proposal status for Customer {customerId}: {payload.EventType}, Proposal {payload.ProposalId}");
            string targetGroup = $"Customer_{customerId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("ProposalStatusUpdate", payload);
        }

        // ── Tầng 8: Customer "Đơn hàng của tôi" Events ──

        public async Task NotifyCustomerShipmentActionAsync(ShipmentActionPayload payload)
        {
            _logger.LogInformation($"[SignalR] Shipment action for Customer: {payload.ShipmentCode} {payload.EventType} ({payload.OldStatus}→{payload.NewStatus})");

            if (payload.CustomerId.HasValue)
            {
                string targetGroup = $"Customer_{payload.CustomerId.Value}";
                await _hubContext.Clients.Group(targetGroup).SendAsync("ShipmentActionUpdate", payload);
            }

            // Also broadcast to Admin dashboard
            await _hubContext.Clients.Group("AdminGroup").SendAsync("ShipmentActionUpdate", payload);
        }

        // ── Tầng 9: Driver "Quản lý chuyến" Events ──

        public async Task NotifyDriverTripEventAsync(DriverTripEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Trip event for Driver {payload.DriverId}: {payload.TripCode} {payload.EventType}");

            string targetGroup = $"Driver_{payload.DriverId}";
            await _hubContext.Clients.Group(targetGroup).SendAsync("DriverTripEvent", payload);
            await _hubContext.Clients.Group("AdminGroup").SendAsync("DriverTripEvent", payload);
        }

        public async Task NotifyDriverShipmentActionAsync(ShipmentActionPayload payload)
        {
            _logger.LogInformation($"[SignalR] Shipment action for Driver: {payload.ShipmentCode} {payload.EventType}");

            if (payload.DriverId.HasValue)
            {
                string targetGroup = $"Driver_{payload.DriverId.Value}";
                await _hubContext.Clients.Group(targetGroup).SendAsync("DriverShipmentActionUpdate", payload);
            }

            // Also notify the customer who owns this shipment
            if (payload.CustomerId.HasValue)
            {
                string customerGroup = $"Customer_{payload.CustomerId.Value}";
                await _hubContext.Clients.Group(customerGroup).SendAsync("ShipmentActionUpdate", payload);
            }

            await _hubContext.Clients.Group("AdminGroup").SendAsync("DriverShipmentActionUpdate", payload);
        }

        // ── Tầng 10: Admin Incident Notifications ──

        public async Task NotifyAdminIncidentAsync(IncidentReportedPayload payload)
        {
            _logger.LogWarning($"[SignalR] Incident reported by Driver {payload.DriverName} on Trip {payload.TripCode}: {payload.IncidentType}");
            await _hubContext.Clients.Group("AdminGroup").SendAsync("IncidentReported", payload);

            // Notify the driver about the report confirmation
            string driverGroup = $"Driver_{payload.DriverId}";
            await _hubContext.Clients.Group(driverGroup).SendAsync("IncidentReported", payload);
        }

        // ── Tầng 11: Quotation & Payment Events (Staff/Quotation/Payment flow) ──

        public async Task SendQuotationToCustomerAsync(Guid customerId, QuotationEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Quotation event {payload.EventType} for Customer {customerId}");

            // Notify customer
            string customerGroup = $"Customer_{customerId}";
            await _hubContext.Clients.Group(customerGroup).SendAsync("QuotationUpdate", payload);

            // Notify staff hub if available (from admin group)
            await _hubContext.Clients.Group("AdminGroup").SendAsync("QuotationUpdate", payload);
        }

        public async Task SendPaymentUpdateToCustomerAsync(Guid customerId, PaymentEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Payment event {payload.EventType} for Customer {customerId}");

            string customerGroup = $"Customer_{customerId}";
            await _hubContext.Clients.Group(customerGroup).SendAsync("PaymentUpdate", payload);

            // Also notify staff
            await _hubContext.Clients.Group("AdminGroup").SendAsync("PaymentUpdate", payload);
        }

        public async Task SendShipmentStatusToStaffAsync(string targetGroup, ShipmentStatusEventPayload payload)
        {
            _logger.LogInformation($"[SignalR] Shipment status change for staff: {payload.OldStatus} → {payload.NewStatus} on Shipment {payload.ShipmentId}");

            // Notify Admin
            await _hubContext.Clients.Group("AdminGroup").SendAsync("ShipmentStatusUpdate", payload);

            // Notify specific hub staff group if provided
            if (!string.IsNullOrEmpty(targetGroup))
            {
                await _hubContext.Clients.Group(targetGroup).SendAsync("ShipmentStatusUpdate", payload);
            }
        }
    }
}
