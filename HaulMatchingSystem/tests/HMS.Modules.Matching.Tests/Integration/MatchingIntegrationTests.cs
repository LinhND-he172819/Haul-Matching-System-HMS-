using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Models;
using HMS.Modules.Matching.Infrastructure;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Shared.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HMS.Modules.Matching.Tests.Integration
{
    public class MatchingIntegrationTests
    {
        [Fact]
        public async Task AcceptAll_CommitsChanges_And_ReleasesRedisLocks()
        {
            var options = new DbContextOptionsBuilder<MatchingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var db = new MatchingDbContext(options);

            // seed vehicle, trip, shipments, tripshipments
            var vehicle = new Vehicle { Id = Guid.NewGuid(), MaxWeightKg = 1000, MaxVolumeCbm = 50 };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = Guid.NewGuid(), VehicleId = vehicle.Id, CurrentLoadWeight = 100, CurrentLoadVolume = 5, Status = "Active" };
            var s1 = new Shipment { Id = Guid.NewGuid(), WeightKg = 200, VolumeCbm = 3, Status = "In_Warehouse", ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X" };
            var s2 = new Shipment { Id = Guid.NewGuid(), WeightKg = 300, VolumeCbm = 4, Status = "In_Warehouse", ReceiverName = "B", ReceiverPhone = "0", DestAddress = "Y" };
            var ts1 = new TripShipment { Id = Guid.NewGuid(), TripId = trip.Id, ShipmentId = s1.Id, DeliverySequence = 1, Status = "Suggested", SuggestedAt = DateTime.UtcNow };
            var ts2 = new TripShipment { Id = Guid.NewGuid(), TripId = trip.Id, ShipmentId = s2.Id, DeliverySequence = 2, Status = "Suggested", SuggestedAt = DateTime.UtcNow };

            db.Vehicles.Add(vehicle);
            db.Trips.Add(trip);
            db.Shipments.AddRange(s1, s2);
            db.TripShipments.AddRange(ts1, ts2);
            await db.SaveChangesAsync();

            var repo = new MatchingRepository(db);
            var redisMock = new Mock<IRedisLockService>();
            redisMock.Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var dispatcherMock = new Mock<IRealtimeDispatcher>();
            dispatcherMock.Setup(d => d.BroadcastMatchingAcceptedAsync(It.IsAny<object>())).Returns(Task.CompletedTask);
            dispatcherMock.Setup(d => d.BroadcastMatchingRejectedAsync(It.IsAny<object>())).Returns(Task.CompletedTask);
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MatchingService>>();

            var svc = new MatchingService(repo, redisMock.Object, dispatcherMock.Object, loggerMock.Object);

            // act
            await svc.AcceptAllAsync(trip.DriverId, CancellationToken.None);

            // assert shipments are matched
            var updatedS1 = await db.Shipments.FindAsync(new object[] { s1.Id });
            var updatedS2 = await db.Shipments.FindAsync(new object[] { s2.Id });
            Assert.NotNull(updatedS1);
            Assert.NotNull(updatedS2);
            Assert.Equal("Matched", updatedS1.Status);
            Assert.Equal("Matched", updatedS2.Status);

            // trip updated
            var updatedTrip = await db.Trips.FindAsync(new object[] { trip.Id });
            Assert.NotNull(updatedTrip);
            Assert.Equal(100 + 200 + 300, updatedTrip.CurrentLoadWeight);

            // redis locks released
            redisMock.Verify(r => r.ReleaseLockAsync($"shipment:{s1.Id}:matching-lock", It.IsAny<CancellationToken>()), Times.Once);
            redisMock.Verify(r => r.ReleaseLockAsync($"shipment:{s2.Id}:matching-lock", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
