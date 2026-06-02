using HMS.Modules.Realtime.Models;

namespace HMS.Modules.Realtime.Interfaces
{
    public interface IRealtimeDispatcher
    {
        // Tầng 1: System
        Task SendSystemNotificationAsync(string message);

        // Tầng 2: Tracking & Anomalies (GPS)
        Task BroadcastVehicleLocationAsync(GpsPayload payload);
        Task SendAnomalyAlertAsync(AnomalyAlertPayload payload);

        // Tầng 3: Business Events (Logistics Status)
        Task BroadcastShipmentStatusAsync(ShipmentStatusEventPayload payload);
        Task BroadcastTripStatusAsync(TripStatusEventPayload payload);

        // Tầng 4: Admin Dashboard Stats (có thể dùng riêng method hoặc tích hợp vào các event trên)
        Task BroadcastAdminStatsAsync(AdminStatsPayload stats);
    }
}
