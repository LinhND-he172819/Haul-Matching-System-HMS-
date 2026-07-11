using HMS.Modules.Warehouse.Application.DTOs;

namespace HMS.Modules.Warehouse.Application.Services;

public interface IHubInventoryService
{
    Task<PagedResult<HubInventoryShipmentDto>> GetInventoryAsync(
        HubInventoryQuery query, CancellationToken ct = default);

    Task<HubInventoryDetailDto?> GetDetailAsync(
        Guid shipmentId, CancellationToken ct = default);

    Task UpdateShipmentAsync(
        Guid shipmentId, UpdateShipmentRequest request, CancellationToken ct = default);

    Task<HubInventoryDashboardDto> GetDashboardSummaryAsync(
        Guid? hubId, CancellationToken ct = default);
}

public class HubInventoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? CargoType { get; set; }
    public Guid? HubId { get; set; }
    public string? Sort { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
}
