using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Moq;
using Xunit;

namespace HMS.Modules.Matching.Tests
{
    /// <summary>
    /// Unit tests for ProposalService (Section 26 of spec).
    /// Covers: CreateProposal, AcceptProposal, RejectProposal, AcceptAllProposals.
    /// </summary>
    public class ProposalServiceTests
    {
        private readonly Mock<IProposalRepository> _repo;
        private readonly Mock<IShipmentStateService> _shipmentStateService;
        private readonly Mock<IRealtimeDispatcher> _dispatcher;
        private readonly Mock<Microsoft.Extensions.Logging.ILogger<ProposalService>> _logger;
        private readonly ProposalService _sut;

        public ProposalServiceTests()
        {
            _repo = new Mock<IProposalRepository>();
            _shipmentStateService = new Mock<IShipmentStateService>();
            _dispatcher = new Mock<IRealtimeDispatcher>();
            _logger = new Mock<Microsoft.Extensions.Logging.ILogger<ProposalService>>();
            _sut = new ProposalService(_repo.Object, _shipmentStateService.Object, _dispatcher.Object, _logger.Object);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // CREATE PROPOSAL TESTS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Fact]
        public async Task CreateProposal_ShipmentNotFound_ThrowsInvalidOperation()
        {
            var repo = new Mock<IProposalRepository>();
            repo.Setup(r => r.GetShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Shipment?)null);
            var sut = new ProposalService(repo.Object, _shipmentStateService.Object, _dispatcher.Object, _logger.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.CreateProposalAsync(Guid.NewGuid(), Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = Guid.NewGuid(), SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_ShipmentNotDraft_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Matched", WeightKg = 10, VolumeCbm = 1 };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(Guid.NewGuid(), Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_ShipmentWeightZero_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 0, VolumeCbm = 1 };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(Guid.NewGuid(), Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_ShipmentVolumeZero_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 0 };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(Guid.NewGuid(), Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_TripPostNotFound_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TripPostRecord?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(tripPostId, Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_TripPostNotOpen_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };
            var tripPost = new TripPostRecord { Id = tripPostId, TripId = Guid.NewGuid(), Status = "Closed" };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(tripPostId, Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_TripPostExpired_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId, TripId = Guid.NewGuid(), Status = "Open",
                AcceptUntil = DateTimeOffset.UtcNow.AddHours(-1) // expired
            };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(tripPostId, Guid.NewGuid(),
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_DuplicatePending_ThrowsInvalidOperation()
        {
            var shipmentId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId, TripId = tripId, Status = "Open",
                AcceptUntil = DateTimeOffset.UtcNow.AddHours(1)
            };
            var existingProposal = new ShipmentProposal
            {
                Id = Guid.NewGuid(), ShipmentId = shipmentId, TripPostId = tripPostId,
                CustomerId = customerId, Status = ProposalStatusConstants.Pending
            };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.HasPendingProposalForShipmentAndTripPostAsync(shipmentId, tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(tripPostId, customerId,
                    new CreateProposalRequest { ShipmentId = shipmentId, SenderName = "A", SenderPhone = "123", PickupAddress = "addr" },
                    CancellationToken.None));
        }

        [Fact]
        public async Task CreateProposal_Success_ReturnsResponseAndSendsSignalR()
        {
            var shipmentId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId, TripId = tripId, Status = "Open",
                AcceptUntil = DateTimeOffset.UtcNow.AddHours(1),
                PickupMode = "DirectPickup"
            };
            var trip = new Trip { Id = tripId, DriverId = driverId, Status = "Active" };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.HasPendingProposalForShipmentAndTripPostAsync(shipmentId, tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repo.Setup(r => r.GetTripByIdAsync(tripId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetPendingByTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());
            _repo.Setup(r => r.AddAsync(It.IsAny<ShipmentProposal>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _sut.CreateProposalAsync(tripPostId, customerId,
                new CreateProposalRequest
                {
                    ShipmentId = shipmentId,
                    SenderName = "Nguyen Van A",
                    SenderPhone = "0901234567",
                    PickupAddress = "123 Le Loi"
                },
                CancellationToken.None);

            Assert.Equal(shipmentId, result.ShipmentId);
            Assert.Equal(tripPostId, result.TripPostId);
            Assert.Equal(ProposalStatusConstants.Pending, result.Status);

            // Verify SignalR was called
            _dispatcher.Verify(d => d.SendNewProposalToDriverAsync(
                driverId,
                It.Is<ProposalEventPayload>(p =>
                    p.EventType == "NewShipmentProposal" &&
                    p.TripPostId == tripPostId)), Times.Once);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // ACCEPT PROPOSAL TESTS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Fact]
        public async Task AcceptProposal_NotFound_ThrowsInvalidOperation()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ShipmentProposal?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_NotPending_ThrowsInvalidOperation()
        {
            var proposalId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Accepted,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid()
            };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_DriverNoActiveTrip_ThrowsInvalidOperation()
        {
            var proposalId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid()
            };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_ProposalNotForThisDriver_ThrowsUnauthorized()
        {
            var proposalId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid()
            };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };
            var tripPost = new TripPostRecord { Id = proposal.TripPostId, TripId = Guid.NewGuid(), Status = "Open", AcceptUntil = DateTimeOffset.UtcNow.AddHours(1) }; // different TripId

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_ShipmentNotDraft_ThrowsInvalidOperation()
        {
            var proposalId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid()
            };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = vehicleId, Status = "Active" };
            var tripPost = new TripPostRecord { Id = proposal.TripPostId, TripId = trip.Id };
            var shipment = new Shipment { Id = proposal.ShipmentId, Status = "Matched", WeightKg = 10, VolumeCbm = 1 };
            var vehicle = new Vehicle { Id = vehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(proposal.ShipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_CapacityExceeded_ThrowsInvalidOperation()
        {
            var proposalId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid()
            };
            var trip = new Trip
            {
                Id = Guid.NewGuid(), DriverId = driverId, VehicleId = vehicleId, Status = "Active",
                CurrentLoadWeight = 900, CurrentLoadVolume = 40, Version = 1
            };
            var tripPost = new TripPostRecord { Id = proposal.TripPostId, TripId = trip.Id };
            var shipment = new Shipment { Id = proposal.ShipmentId, Status = "Draft", WeightKg = 200, VolumeCbm = 15 };
            var vehicle = new Vehicle { Id = vehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(proposal.ShipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(proposal.ShipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None));
        }

        [Fact]
        public async Task AcceptProposal_Success_TransitionsAndSendsNotifications()
        {
            var proposalId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = shipmentId, TripPostId = Guid.NewGuid(),
                CustomerId = customerId
            };
            var trip = new Trip
            {
                Id = tripId, DriverId = driverId, VehicleId = vehicleId, Status = "Active",
                CurrentLoadWeight = 100, CurrentLoadVolume = 5, Version = 1
            };
            var tripPost = new TripPostRecord { Id = proposal.TripPostId, TripId = tripId, Status = "Open", AcceptUntil = DateTimeOffset.UtcNow.AddHours(1) };
            var shipment = new Shipment { Id = shipmentId, Status = "Draft", WeightKg = 50, VolumeCbm = 5 };
            var vehicle = new Vehicle { Id = vehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repo.Setup(r => r.GetPendingByShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());
            _repo.Setup(r => r.GetUnderlyingConnection())
                .Returns((System.Data.Common.DbConnection?)null!);
            _repo.Setup(r => r.GetUnderlyingTransaction())
                .Returns((System.Data.Common.DbTransaction?)null!);
            _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None);

            Assert.Equal(proposalId, result.ProposalId);
            Assert.Equal(ProposalStatusConstants.Accepted, result.Status);

            // Verify shipment state transition was called
            _shipmentStateService.Verify(s => s.TransitionAsync(
                shipmentId,
                ShipmentStatus.Matched,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                driverId,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify trip capacity update
            _repo.Verify(r => r.UpdateTripLoadAsync(
                tripId, 50, 5, 1, It.IsAny<CancellationToken>()), Times.Once);

            // Verify SignalR capacity notification to driver
            _dispatcher.Verify(d => d.SendTripCapacityUpdatedToDriverAsync(
                driverId,
                It.Is<ProposalEventPayload>(p => p.EventType == "TripCapacityUpdated")), Times.Once);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // REJECT PROPOSAL TESTS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Fact]
        public async Task RejectProposal_NotFound_ThrowsInvalidOperation()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ShipmentProposal?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.RejectProposalAsync(Guid.NewGuid(), Guid.NewGuid(),
                    new RejectProposalRequest { Reason = "too heavy" }, CancellationToken.None));
        }

        [Fact]
        public async Task RejectProposal_Success_SendsNotification()
        {
            var proposalId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var proposal = new ShipmentProposal
            {
                Id = proposalId, Status = ProposalStatusConstants.Pending,
                ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid(),
                CustomerId = customerId
            };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };
            var tripPost = new TripPostRecord { Id = proposal.TripPostId, TripId = trip.Id };
            var shipment = new Shipment { Id = proposal.ShipmentId, Status = "Draft", WeightKg = 10, VolumeCbm = 1 };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(proposal.ShipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _sut.RejectProposalAsync(proposalId, driverId,
                new RejectProposalRequest { Reason = "QuÃ¡ táº£i trá»ng" }, CancellationToken.None);

            Assert.Equal(proposalId, result.ProposalId);
            Assert.Equal(ProposalStatusConstants.Rejected, result.Status);

            // Verify SignalR customer notification
            _dispatcher.Verify(d => d.SendProposalStatusToCustomerAsync(
                customerId,
                It.Is<ProposalEventPayload>(p =>
                    p.EventType == "ProposalRejected" &&
                    p.ProposalId == proposalId)), Times.Once);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // ACCEPT ALL TESTS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Fact]
        public async Task AcceptAll_NoActiveTrip_ThrowsInvalidOperation()
        {
            _repo.Setup(r => r.GetActiveTripForDriverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptAllProposalsAsync(Guid.NewGuid(),
                    new AcceptAllProposalsRequest { TripId = Guid.NewGuid() },
                    CancellationToken.None));
        }

        [Fact]
        public async Task AcceptAll_NoPendingProposals_ThrowsInvalidOperation()
        {
            var driverId = Guid.NewGuid();
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };
            var vehicle = new Vehicle { Id = trip.VehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };

            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetVehicleAsync(trip.VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.GetPendingByDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptAllProposalsAsync(driverId,
                    new AcceptAllProposalsRequest { TripId = trip.Id },
                    CancellationToken.None));
        }

        [Fact]
        public async Task AcceptAll_TotalCapacityExceeded_ThrowsInvalidOperation()
        {
            var driverId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var trip = new Trip
            {
                Id = tripId, DriverId = driverId, VehicleId = vehicleId, Status = "Active",
                CurrentLoadWeight = 800, CurrentLoadVolume = 40, Version = 1
            };
            var vehicle = new Vehicle { Id = vehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };
            var proposals = new List<ShipmentProposal>
            {
                new() { Id = Guid.NewGuid(), ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid(), Status = ProposalStatusConstants.Pending, CustomerId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), ShipmentId = Guid.NewGuid(), TripPostId = Guid.NewGuid(), Status = ProposalStatusConstants.Pending, CustomerId = Guid.NewGuid() }
            };

            // Each shipment 150kg / 8mÂ³ â†’ total 300kg â†’ exceeds 1000-800=200
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.GetPendingByDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposals);
            _repo.Setup(r => r.GetShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new Shipment
                {
                    Id = id, Status = "Draft", WeightKg = 150, VolumeCbm = 8
                });
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptAllProposalsAsync(driverId,
                    new AcceptAllProposalsRequest { TripId = tripId },
                    CancellationToken.None));
        }

        [Fact]
        public async Task AcceptAll_Success_AcceptsAllAndSendsNotification()
        {
            var driverId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();
            var trip = new Trip
            {
                Id = tripId, DriverId = driverId, VehicleId = vehicleId, Status = "Active",
                CurrentLoadWeight = 100, CurrentLoadVolume = 5, Version = 1
            };
            var vehicle = new Vehicle { Id = vehicleId, MaxWeightKg = 1000, MaxVolumeCbm = 50 };
            var proposal1Id = Guid.NewGuid();
            var proposal2Id = Guid.NewGuid();
            var shipment1Id = Guid.NewGuid();
            var shipment2Id = Guid.NewGuid();
            var proposals = new List<ShipmentProposal>
            {
                new() { Id = proposal1Id, ShipmentId = shipment1Id, TripPostId = Guid.NewGuid(), Status = ProposalStatusConstants.Pending, CustomerId = Guid.NewGuid() },
                new() { Id = proposal2Id, ShipmentId = shipment2Id, TripPostId = Guid.NewGuid(), Status = ProposalStatusConstants.Pending, CustomerId = Guid.NewGuid() }
            };

            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetVehicleAsync(vehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.GetPendingByDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposals);
            _repo.Setup(r => r.GetShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new Shipment
                {
                    Id = id, Status = "Draft", WeightKg = 50, VolumeCbm = 5
                });
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repo.Setup(r => r.GetPendingByShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());
            _repo.Setup(r => r.GetUnderlyingConnection()).Returns((System.Data.Common.DbConnection?)null!);
            _repo.Setup(r => r.GetUnderlyingTransaction()).Returns((System.Data.Common.DbTransaction?)null!);
            _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await _sut.AcceptAllProposalsAsync(driverId,
                new AcceptAllProposalsRequest { TripId = tripId },
                CancellationToken.None);

            Assert.Equal(tripId, result.TripId);

            // Verify trip capacity update with total weight/volume
            _repo.Verify(r => r.UpdateTripLoadAsync(
                tripId, 100, 10, 1, It.IsAny<CancellationToken>()), Times.Once);

            // Verify 2 shipment transitions
            _shipmentStateService.Verify(s => s.TransitionAsync(
                It.IsAny<Guid>(), ShipmentStatus.Matched,
                It.IsAny<object?>(), It.IsAny<object?>(), driverId,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            // Verify SignalR notification
            _dispatcher.Verify(d => d.SendTripCapacityUpdatedToDriverAsync(
                driverId,
                It.Is<ProposalEventPayload>(p => p.EventType == "TripCapacityUpdated")), Times.Once);
        }
    }
}
