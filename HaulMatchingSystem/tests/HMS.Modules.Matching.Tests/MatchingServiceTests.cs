using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Shared.Core.Interfaces;
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

        [Fact]
        public async Task GetSuggestions_GeneratesSpatialSuggestions_WhenNoSuggestedShipmentsExist()
        {
            var driverId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                DriverId = driverId,
                VehicleId = vehicleId,
                CurrentLoadWeight = 100,
                CurrentLoadVolume = 4,
                Status = "Active"
            };
            var vehicle = new Vehicle
            {
                Id = vehicleId,
                MaxWeightKg = 1000,
                MaxVolumeCbm = 40
            };
            var firstShipment = new Shipment
            {
                Id = Guid.NewGuid(),
                ReceiverName = "Forward first",
                WeightKg = 100,
                VolumeCbm = 2,
                Status = "In_Warehouse"
            };
            var secondShipment = new Shipment
            {
                Id = Guid.NewGuid(),
                ReceiverName = "Forward second",
                WeightKg = 120,
                VolumeCbm = 3,
                Status = "In_Warehouse"
            };
            var spatialCandidates = new List<SpatialShipmentCandidate>
            {
                new() { Shipment = secondShipment, RoutePosition = 0.8, DistanceMeters = 900 },
                new() { Shipment = firstShipment, RoutePosition = 0.2, DistanceMeters = 500 }
            };
            var repo = new Mock<IMatchingRepository>();
            var redis = new Mock<HMS.Modules.Matching.Infrastructure.Redis.IRedisLockService>();
            var dispatcher = new Mock<IRealtimeDispatcher>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<MatchingService>>();
            List<TripShipment> capturedSuggestions = [];

            repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            repo.Setup(r => r.GetSuggestedTripShipmentsAsync(trip.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            repo.Setup(r => r.GetSpatialShipmentCandidatesAsync(
                    trip.Id,
                    vehicle.MaxWeightKg - trip.CurrentLoadWeight,
                    vehicle.MaxVolumeCbm - trip.CurrentLoadVolume,
                    5000,
                    20,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(spatialCandidates);
            repo.Setup(r => r.AddTripShipmentSuggestionsAsync(It.IsAny<IEnumerable<TripShipment>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<TripShipment>, CancellationToken>((suggestions, _) => capturedSuggestions = suggestions.ToList())
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.GetShipmentsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    new[] { firstShipment, secondShipment }
                        .Where(shipment => ids.Contains(shipment.Id))
                        .ToList());
            var svc = new MatchingService(repo.Object, redis.Object, dispatcher.Object, logger.Object);

            var res = await svc.GetSuggestionsForDriverAsync(driverId, CancellationToken.None);

            Assert.NotNull(res);
            Assert.Equal(2, res.Shipments.Count);
            Assert.Equal(firstShipment.Id, res.Shipments[0].ShipmentId);
            Assert.Equal(secondShipment.Id, res.Shipments[1].ShipmentId);
            Assert.Equal(1, capturedSuggestions[0].DeliverySequence);
            Assert.Equal(firstShipment.Id, capturedSuggestions[0].ShipmentId);
            Assert.Equal(2, capturedSuggestions[1].DeliverySequence);
            Assert.Equal(secondShipment.Id, capturedSuggestions[1].ShipmentId);
            Assert.Equal(500, res.Shipments[0].RouteDistanceMeters);
            Assert.Equal(0.2, res.Shipments[0].RoutePosition);
        }
    }
}
