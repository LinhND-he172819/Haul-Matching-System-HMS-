using HMS.Modules.Matching.Application.DTOs;

namespace HMS.Modules.Matching.Core.Interfaces
{
    /// <summary>
    /// Service for Driver External Shipment Declaration.
    /// Drivers can declare shipments they encounter outside the system.
    /// The trip is automatically determined from the driver's active trip.
    /// </summary>
    public interface IDriverExternalShipmentService
    {
        /// <summary>
        /// Create a new external shipment and proposal for the driver's active trip.
        /// </summary>
        Task<CreateExternalShipmentResponse> CreateExternalShipmentAsync(
            Guid driverId, CreateExternalShipmentRequest request, CancellationToken ct);

        /// <summary>
        /// List all external shipments declared by the driver.
        /// </summary>
        Task<PagedResult<ExternalShipmentListItem>> GetExternalShipmentsAsync(
            Guid driverId, string? status, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Get detail of a specific external shipment.
        /// </summary>
        Task<ExternalShipmentDetail?> GetExternalShipmentDetailAsync(
            Guid driverId, Guid shipmentId, CancellationToken ct);
    }
}
