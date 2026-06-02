using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Realtime.Hubs
{
    public class HmsFleetHub : Hub
    {
        private readonly ILogger<HmsFleetHub> _logger;

        public HmsFleetHub(ILogger<HmsFleetHub> logger)
        {
            _logger = logger;
        }

        // khi client (app/web) kết nối thành công
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"[SignalR] Client connected: {Context.ConnectionId}");
            // test
            await Clients.Caller.SendAsync("ReceiveSystemMessage", "Welcome to HMS Realtime Hub!");
            await base.OnConnectedAsync();
        }

        // khi client ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"[SignalR] Client disconnected: {Context.ConnectionId}. Reason: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }

        // iter 3 skeleton method để nhận GPS từ client
        public async Task PingLocation(double lat, double lng)
        {
            _logger.LogInformation($"[SignalR] Ping from {Context.ConnectionId}: Lat {lat}, Lng {lng}");
            // Iter 3: Sẽ ghi vào Redis Write-Behind Cache ở đây
        }
    }
}
