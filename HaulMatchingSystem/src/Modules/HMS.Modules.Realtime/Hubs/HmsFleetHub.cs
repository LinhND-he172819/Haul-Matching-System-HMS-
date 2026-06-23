using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace HMS.Modules.Realtime.Hubs
{
    [Authorize]
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
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "AdminGroup");
            }
            else if (userRole == "Driver")
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Driver_{userId}");
                }
            }

            _logger.LogInformation($"[SignalR] Client kết nối: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        // khi client ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Admin")
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminGroup");
            }
            _logger.LogInformation($"[SignalR] Client ngắt kết nối: {Context.ConnectionId}. Lý do: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }

        // iter 3 skeleton method để nhận GPS từ client
        public async Task PingLocation(double lat, double lng)
        {
            _logger.LogInformation($"[SignalR] Ping từ {Context.ConnectionId}: Lat {lat}, Lng {lng}");
            // Iter 3: Sẽ ghi vào Redis Write-Behind Cache ở đây
        }
    }
}
