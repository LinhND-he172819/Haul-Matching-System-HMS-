using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Modules.Realtime.Interfaces;
using Moq;
using Xunit;

namespace HMS.Modules.Matching.Tests
{
    public class MatchingServiceTests
    {
        [Fact]
        public async Task GetSuggestions_ReturnsNull_WhenNoActiveTrip()
        {
            var repo = new Mock<IMatchingRepository>();
            var redis = new Mock<HMS.Modules.Matching.Infrastructure.Redis.IRedisLockService>();
            var dispatcher = new Mock<IRealtimeDispatcher>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<MatchingService>>();

            repo.Setup(r => r.GetActiveTripForDriverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Trip?)null);

            var svc = new MatchingService(repo.Object, redis.Object, dispatcher.Object, logger.Object);

            var res = await svc.GetSuggestionsForDriverAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(res);
        }
    }
}
