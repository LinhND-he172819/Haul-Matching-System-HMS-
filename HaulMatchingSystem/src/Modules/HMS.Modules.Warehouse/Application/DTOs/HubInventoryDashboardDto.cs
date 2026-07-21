namespace HMS.Modules.Warehouse.Application.DTOs;

public class HubInventoryDashboardDto
{
    public int TotalShipment { get; set; }
    public int InWarehouse { get; set; }
    public int Matched { get; set; }
    public int ReadyForDispatch { get; set; }
    public int Expired { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalVolume { get; set; }
}
