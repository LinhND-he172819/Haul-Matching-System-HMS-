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

        // Tầng 4: Admin Dashboard Stats
        Task BroadcastAdminStatsAsync(AdminStatsPayload stats);

        // Tầng 5: User-Specific Notifications
        Task SendDriverMatchingNotificationAsync(MatchingNotificationPayload payload);

        // Tầng 6: Customer Notifications
        Task SendCustomerStatusNotificationAsync(CustomerStatusPayload payload);

        // Tầng 7: Shipment Proposal Events (Customer→Driver flow)
        Task SendNewProposalToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendProposalCancelledToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendTripCapacityUpdatedToDriverAsync(Guid driverId, ProposalEventPayload payload);
        Task SendProposalStatusToCustomerAsync(Guid customerId, ProposalEventPayload payload);

        // Tầng 8: Customer "Đơn hàng của tôi" Events
        Task NotifyCustomerShipmentActionAsync(ShipmentActionPayload payload);

        // Tầng 9: Driver "Quản lý chuyến" Events
        Task NotifyDriverTripEventAsync(DriverTripEventPayload payload);
        Task NotifyDriverShipmentActionAsync(ShipmentActionPayload payload);

        // Tầng 10: Admin Incident Notifications
        Task NotifyAdminIncidentAsync(IncidentReportedPayload payload);

        // Tầng 11: Quotation & Payment Events (Staff/Quotation/Payment flow)
        Task SendQuotationToCustomerAsync(Guid customerId, QuotationEventPayload payload);
        Task SendPaymentUpdateToCustomerAsync(Guid customerId, PaymentEventPayload payload);
        Task SendShipmentStatusToStaffAsync(string targetGroup, ShipmentStatusEventPayload payload);
    }
}
