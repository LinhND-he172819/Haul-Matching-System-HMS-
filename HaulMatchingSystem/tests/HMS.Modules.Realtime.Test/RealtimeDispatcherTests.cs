using HMS.Modules.Realtime.Hubs;
using HMS.Modules.Realtime.Models;
using HMS.Modules.Realtime.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace HMS.Modules.Realtime.Tests
{
    public class RealtimeDispatcherTests 
    {
        [Fact]
        public async Task SendSystemNotification_ShouldInvoke_SignalR_SendAsync()
        {
            // 1. Arrange (Chuẩn bị dữ liệu và Mock)
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();

            mockClients.Setup(clients => clients.All).Returns(mockClientProxy.Object);

            var mockHubContext = new Mock<IHubContext<HmsFleetHub>>();
            mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            var mockLogger = new Mock<ILogger<RealtimeDispatcher>>();

            var dispatcher = new RealtimeDispatcher(mockHubContext.Object, mockLogger.Object);
            var testMessage = "Hệ thống tạm đóng để bảo hành!";

            // 2. Act (Thực thi)
            await dispatcher.SendSystemNotificationAsync(testMessage);

            // 3. Assert (Kiểm chứng)
            // Đảm bảo rằng hàm SendCoreAsync của SignalR đã được gọi đúng 1 lần với sự kiện "ReceiveSystemMessage"
            mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "ReceiveSystemMessage",
                    It.Is<object[]>(o => o != null && o.Length == 1 && (string)o[0] == testMessage),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task BroadcastAdminStats_ShouldSend_To_AdminGroup_Only()
        {
            // 1. Arrange
            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();

            // Mock việc trỏ tới Group có tên là "AdminGroup"
            mockClients.Setup(clients => clients.Group("AdminGroup")).Returns(mockClientProxy.Object);

            var mockHubContext = new Mock<IHubContext<HmsFleetHub>>();
            mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            var mockLogger = new Mock<ILogger<RealtimeDispatcher>>();
            var dispatcher = new RealtimeDispatcher(mockHubContext.Object, mockLogger.Object);

            var testStats = new AdminStatsPayload
            {
                ActiveTripCount = 15,
                InTransitShipments = 100
            };

            // 2. Act
            await dispatcher.BroadcastAdminStatsAsync(testStats);

            // 3. Assert
            // Đảm bảo rằng hàm SendCoreAsync được gọi ĐÚNG TRÊN CÁI PROXY CỦA "AdminGroup"
            mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "ReceiveAdminStats",
                    It.Is<object[]>(o => o != null && o.Length == 1 && ((AdminStatsPayload)o[0]).ActiveTripCount == 15),
                    default),
                Times.Once);

            // Đảm bảo rằng nó KHÔNG hề gọi hàm gửi cho All
            mockClients.Verify(clients => clients.All, Times.Never);
        }
    }   
}