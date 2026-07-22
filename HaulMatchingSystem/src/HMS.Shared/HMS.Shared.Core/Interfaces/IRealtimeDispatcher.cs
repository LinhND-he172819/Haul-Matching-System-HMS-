using HMS.Shared.Core.Models.Realtime;

namespace HMS.Shared.Core.Interfaces
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
        Task BroadcastMatchingAcceptedAsync(object payload);
        Task BroadcastMatchingRejectedAsync(object payload);

        // Tầng 4: Admin Dashboard Stats (có thể dùng riêng method hoặc tích hợp vào các event trên)
        Task BroadcastAdminStatsAsync(AdminStatsPayload stats);

        // Tầng 5: User-Specific Notifications (có thể dùng riêng method hoặc tích hợp vào các event trên)
        Task SendDriverMatchingNotificationAsync(MatchingNotificationPayload payload);

        // Tầng 6: Customer Notifications
        Task SendCustomerStatusNotificationAsync(CustomerStatusPayload payload);

        // Tầng 7: Shipment Proposal Events (Customer→Driver flow)
        Task SendNewProposalToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendProposalCancelledToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendTripCapacityUpdatedToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendProposalStatusToCustomerAsync(Guid customerId, ProposalEventPayload payload);
    }
}
