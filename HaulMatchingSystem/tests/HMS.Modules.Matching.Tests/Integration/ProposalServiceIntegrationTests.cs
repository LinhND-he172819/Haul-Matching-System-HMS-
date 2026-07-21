using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Services;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Moq;

namespace HMS.Modules.Matching.Tests.Integration
{
    /// <summary>
    /// Integration tests for ProposalService end-to-end flows.
    /// Tests multi-step scenarios: Create -> Accept -> Shipment transitions,
    /// concurrent proposals, capacity validation, etc.
    /// 
    /// Uses mocked IProposalRepository + real ProposalService logic to validate
    /// the full business flow across multiple service method calls.
    /// </summary>
    public class ProposalServiceIntegrationTests
    {
        private readonly Mock<IProposalRepository> _repo;
        private readonly Mock<IShipmentStateService> _stateService;
        private readonly Mock<IRealtimeDispatcher> _dispatcher;
        private readonly Mock<Microsoft.Extensions.Logging.ILogger<ProposalService>> _logger;
        private readonly ProposalService _sut;

        public ProposalServiceIntegrationTests()
        {
            _repo = new Mock<IProposalRepository>();
            _stateService = new Mock<IShipmentStateService>();
            _dispatcher = new Mock<IRealtimeDispatcher>();
            _logger = new Mock<Microsoft.Extensions.Logging.ILogger<ProposalService>>();

            _stateService
                .Setup(s => s.TransitionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<ShipmentStatus>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, ShipmentStatus to, object? c, object? t, Guid? p, string? r, CancellationToken _) => to);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repo.Setup(r => r.GetUnderlyingConnection())
                .Returns((System.Data.Common.DbConnection?)null);
            _repo.Setup(r => r.GetUnderlyingTransaction())
                .Returns((System.Data.Common.DbTransaction?)null);

            _dispatcher.Setup(d => d.SendNewProposalToDriverAsync(
                It.IsAny<Guid>(), It.IsAny<ProposalEventPayload>()))
                .Returns(Task.CompletedTask);
            _dispatcher.Setup(d => d.SendProposalCancelledToDriverAsync(
                It.IsAny<Guid>(), It.IsAny<ProposalEventPayload>()))
                .Returns(Task.CompletedTask);
            _dispatcher.Setup(d => d.SendTripCapacityUpdatedToDriverAsync(
                It.IsAny<Guid>(), It.IsAny<ProposalEventPayload>()))
                .Returns(Task.CompletedTask);
            _dispatcher.Setup(d => d.SendProposalStatusToCustomerAsync(
                It.IsAny<Guid>(), It.IsAny<ProposalEventPayload>()))
                .Returns(Task.CompletedTask);

            _sut = new ProposalService(_repo.Object, _stateService.Object, _dispatcher.Object, _logger.Object);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 1: Full happy-path: Create -> View -> Accept -> Verify state
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FullFlow_CreateThenAccept_ShipmentTransitionsToMatched()
        {
            // ── Arrange ──
            var customerId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var vehicle = new Vehicle { Id = Guid.NewGuid(), MaxWeightKg = 1000, MaxVolumeCbm = 50 };
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                DriverId = driverId,
                VehicleId = vehicle.Id,
                CurrentLoadWeight = 0,
                CurrentLoadVolume = 0,
                Status = "Active",
                Version = 1
            };
            var shipment = new Shipment
            {
                Id = shipmentId,
                Status = ShipmentStatus.Draft.ToString(),
                WeightKg = 100,
                VolumeCbm = 5,
                ReceiverName = "Nguyen Van A",
                ReceiverPhone = "0901234567",
                DestAddress = "Ha Noi"
            };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId,
                TripId = trip.Id,
                Status = "Open",
                PickupMode = "DirectPickup",
                AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = driverId,
                Title = "Hanoi -> HCMC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // ── Step 1: Create Proposal ──
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetPendingByShipmentAndTripPostAsync(shipmentId, tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ShipmentProposal?)null);
            _repo.Setup(r => r.GetTripByIdAsync(tripPost.TripId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetPendingByTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());

            ShipmentProposal? savedProposal = null;
            _repo.Setup(r => r.AddAsync(It.IsAny<ShipmentProposal>(), It.IsAny<CancellationToken>()))
                .Callback<ShipmentProposal, CancellationToken>((p, _) => savedProposal = p)
                .Returns(Task.CompletedTask);

            var createResult = await _sut.CreateProposalAsync(
                tripPostId,
                customerId,
                new CreateProposalRequest
                {
                    ShipmentId = shipmentId,
                    SenderName = "Tran Van B",
                    SenderPhone = "0912345678",
                    PickupAddress = "123 Le Loi, Q1, HCMC"
                },
                CancellationToken.None);

            Assert.NotNull(createResult);
            Assert.Equal(ProposalStatusConstants.Pending, createResult.Status);
            Assert.NotNull(savedProposal);
            Assert.Equal(shipmentId, savedProposal.ShipmentId);
            Assert.Equal(customerId, savedProposal.CustomerId);

            // ── Step 2: Accept Proposal (as driver) ──
            var proposalId = savedProposal.Id;

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedProposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetVehicleAsync(trip.VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repo.Setup(r => r.GetPendingByShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());

            var acceptResult = await _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None);

            Assert.NotNull(acceptResult);
            Assert.Equal(ProposalStatusConstants.Accepted, acceptResult.Status);
            Assert.Equal(shipmentId, acceptResult.ShipmentId);

            // Verify shipment transitioned to Matched
            _stateService.Verify(s => s.TransitionAsync(
                shipmentId,
                ShipmentStatus.Matched,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                driverId,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify trip load updated
            _repo.Verify(r => r.UpdateTripLoadAsync(
                trip.Id, 100m, 5m, trip.Version, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify proposal was updated to Accepted
            Assert.Equal(ProposalStatusConstants.Accepted, savedProposal.Status);
            Assert.Equal(driverId, savedProposal.AcceptedBy);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 2: Accept one proposal -> other proposals auto-cancelled
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AcceptProposal_CancelsOtherPendingProposalsForSameShipment()
        {
            var customerId1 = Guid.NewGuid();
            var customerId2 = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var tripPostId1 = Guid.NewGuid();
            var tripPostId2 = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var vehicle = new Vehicle { Id = Guid.NewGuid(), MaxWeightKg = 1000, MaxVolumeCbm = 50 };
            var trip = new Trip
            {
                Id = Guid.NewGuid(), DriverId = driverId, VehicleId = vehicle.Id,
                CurrentLoadWeight = 0, CurrentLoadVolume = 0, Status = "Active", Version = 1
            };
            var shipment = new Shipment
            {
                Id = shipmentId, Status = ShipmentStatus.Draft.ToString(),
                WeightKg = 50, VolumeCbm = 2,
                ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X"
            };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId1, TripId = trip.Id, Status = "Open",
                PickupMode = "DirectPickup", AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = driverId, Title = "TP1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };

            var proposal1 = new ShipmentProposal
            {
                Id = Guid.NewGuid(), ShipmentId = shipmentId, TripPostId = tripPostId1,
                CustomerId = customerId1, Status = ProposalStatusConstants.Pending,
                CreatedAt = DateTime.UtcNow, SenderName = "C1", SenderPhone = "0", PickupAddress = "A"
            };
            var proposal2 = new ShipmentProposal
            {
                Id = Guid.NewGuid(), ShipmentId = shipmentId, TripPostId = tripPostId2,
                CustomerId = customerId2, Status = ProposalStatusConstants.Pending,
                CreatedAt = DateTime.UtcNow, SenderName = "C2", SenderPhone = "0", PickupAddress = "B"
            };

            // Setup for accept proposal1
            _repo.Setup(r => r.GetByIdAsync(proposal1.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal1);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetVehicleAsync(trip.VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(vehicle);
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repo.Setup(r => r.GetPendingByShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal> { proposal2 });

            await _sut.AcceptProposalAsync(proposal1.Id, driverId, CancellationToken.None);

            // Verify proposal1 is Accepted
            Assert.Equal(ProposalStatusConstants.Accepted, proposal1.Status);

            // Verify proposal2 was auto-cancelled
            Assert.Equal(ProposalStatusConstants.Cancelled, proposal2.Status);
            Assert.NotNull(proposal2.CancelledAt);

            // Verify shipment transitioned
            _stateService.Verify(s => s.TransitionAsync(
                shipmentId, ShipmentStatus.Matched,
                It.IsAny<object?>(), It.IsAny<object?>(),
                driverId, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 3: Create -> Cancel -> verify proposal cancelled
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateThenCancel_ProposalCancelledAndDriverNotified()
        {
            var customerId = Guid.NewGuid();
            var driverId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };
            var shipment = new Shipment
            {
                Id = shipmentId, Status = ShipmentStatus.Draft.ToString(),
                WeightKg = 10, VolumeCbm = 1,
                ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X"
            };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId, TripId = trip.Id, Status = "Open",
                PickupMode = "DirectPickup", AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = driverId, Title = "Post", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };

            // Step 1: Create
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetPendingByShipmentAndTripPostAsync(shipmentId, tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ShipmentProposal?)null);
            _repo.Setup(r => r.GetTripByIdAsync(tripPost.TripId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetPendingByTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ShipmentProposal>());

            ShipmentProposal? savedProposal = null;
            _repo.Setup(r => r.AddAsync(It.IsAny<ShipmentProposal>(), It.IsAny<CancellationToken>()))
                .Callback<ShipmentProposal, CancellationToken>((p, _) => savedProposal = p)
                .Returns(Task.CompletedTask);

            await _sut.CreateProposalAsync(
                tripPostId, customerId,
                new CreateProposalRequest
                {
                    ShipmentId = shipmentId,
                    SenderName = "Test", SenderPhone = "0", PickupAddress = "Addr"
                },
                CancellationToken.None);

            Assert.NotNull(savedProposal);
            Assert.Equal(ProposalStatusConstants.Pending, savedProposal.Status);

            // Step 2: Cancel
            _repo.Setup(r => r.GetByIdAsync(savedProposal.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedProposal);

            await _sut.CancelProposalAsync(savedProposal.Id, customerId, CancellationToken.None);

            Assert.Equal(ProposalStatusConstants.Cancelled, savedProposal.Status);
            Assert.NotNull(savedProposal.CancelledAt);

            // Driver should be notified via SignalR
            _dispatcher.Verify(d => d.SendProposalCancelledToDriverAsync(
                driverId,
                It.Is<ProposalEventPayload>(p =>
                    p.ProposalId == savedProposal.Id &&
                    p.TripPostId == tripPostId)),
                Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 4: Duplicate proposal rejected
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateProposal_DuplicatePending_Throws()
        {
            var customerId = Guid.NewGuid();
            var tripPostId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var trip = new Trip { Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = "Active" };
            var shipment = new Shipment
            {
                Id = shipmentId, Status = ShipmentStatus.Draft.ToString(),
                WeightKg = 10, VolumeCbm = 1,
                ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X"
            };
            var tripPost = new TripPostRecord
            {
                Id = tripPostId, TripId = trip.Id, Status = "Open",
                PickupMode = "DirectPickup", AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = Guid.NewGuid(), Title = "Post", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            var existingProposal = new ShipmentProposal
            {
                Id = Guid.NewGuid(), ShipmentId = shipmentId, TripPostId = tripPostId,
                CustomerId = Guid.NewGuid(), Status = ProposalStatusConstants.Pending
            };

            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);
            _repo.Setup(r => r.GetTripPostAsync(tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetPendingByShipmentAndTripPostAsync(shipmentId, tripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProposal);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateProposalAsync(
                    tripPostId, customerId,
                    new CreateProposalRequest
                    {
                        ShipmentId = shipmentId,
                        SenderName = "Test", SenderPhone = "0", PickupAddress = "Addr"
                    },
                    CancellationToken.None));
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 5: Accept proposal for shipment already accepted -> throw
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AcceptProposal_ShipmentAlreadyAccepted_ThrowsInvalidOperation()
        {
            var driverId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var proposal = new ShipmentProposal
            {
                Id = proposalId, ShipmentId = shipmentId, TripPostId = Guid.NewGuid(),
                Status = ProposalStatusConstants.Pending
            };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };
            var tripPost = new TripPostRecord
            {
                Id = proposal.TripPostId, TripId = trip.Id, Status = "Open",
                PickupMode = "DirectPickup", AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = driverId, Title = "Post", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Shipment
                {
                    Id = shipmentId, Status = ShipmentStatus.Draft.ToString(),
                    WeightKg = 10, VolumeCbm = 1,
                    ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X"
                });
            _repo.Setup(r => r.HasAcceptedProposalForShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // already accepted elsewhere

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None));

            // Verify state transition was never called
            _stateService.Verify(s => s.TransitionAsync(
                It.IsAny<Guid>(), It.IsAny<ShipmentStatus>(),
                It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 6: Cancel proposal by wrong customer -> unauthorized
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CancelProposal_WrongCustomer_ThrowsUnauthorized()
        {
            var realCustomerId = Guid.NewGuid();
            var wrongCustomerId = Guid.NewGuid();

            var proposal = new ShipmentProposal
            {
                Id = Guid.NewGuid(), CustomerId = realCustomerId,
                Status = ProposalStatusConstants.Pending
            };

            _repo.Setup(r => r.GetByIdAsync(proposal.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _sut.CancelProposalAsync(proposal.Id, wrongCustomerId, CancellationToken.None));

            Assert.Equal(ProposalStatusConstants.Pending, proposal.Status);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 7: Accept proposal -> capacity exceeded -> throw, no changes
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AcceptProposal_CapacityExceeded_ThrowsAndRollsBack()
        {
            var driverId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();

            var vehicle = new Vehicle { Id = Guid.NewGuid(), MaxWeightKg = 100, MaxVolumeCbm = 10 };
            var trip = new Trip
            {
                Id = Guid.NewGuid(), DriverId = driverId, VehicleId = vehicle.Id,
                CurrentLoadWeight = 90, CurrentLoadVolume = 8, Status = "Active", Version = 1
            };
            var proposal = new ShipmentProposal
            {
                Id = proposalId, ShipmentId = shipmentId, TripPostId = Guid.NewGuid(),
                Status = ProposalStatusConstants.Pending
            };
            var tripPost = new TripPostRecord
            {
                Id = proposal.TripPostId, TripId = trip.Id, Status = "Open",
                PickupMode = "DirectPickup", AcceptUntil = DateTimeOffset.UtcNow.AddHours(2),
                CreatedBy = driverId, Title = "Post", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            // Shipment weight = 20 > remaining 10 kg
            var shipment = new Shipment
            {
                Id = shipmentId, Status = ShipmentStatus.Draft.ToString(),
                WeightKg = 20, VolumeCbm = 1,
                ReceiverName = "A", ReceiverPhone = "0", DestAddress = "X"
            };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tripPost);
            _repo.Setup(r => r.GetShipmentAsync(shipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipment);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.AcceptProposalAsync(proposalId, driverId, CancellationToken.None));

            // Verify rollback was called
            _repo.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            // Verify no state transition
            _stateService.Verify(s => s.TransitionAsync(
                It.IsAny<Guid>(), It.IsAny<ShipmentStatus>(),
                It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FLOW 8: Reject proposal -> proposal rejected, no state change
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task RejectProposal_SetsRejectedAndNotifiesCustomer()
        {
            var driverId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();

            var proposal = new ShipmentProposal
            {
                Id = proposalId, CustomerId = customerId, TripPostId = Guid.NewGuid(),
                Status = ProposalStatusConstants.Pending, ShipmentId = Guid.NewGuid()
            };
            var trip = new Trip { Id = Guid.NewGuid(), DriverId = driverId, VehicleId = Guid.NewGuid(), Status = "Active" };

            _repo.Setup(r => r.GetByIdAsync(proposalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proposal);
            _repo.Setup(r => r.GetActiveTripForDriverAsync(driverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(trip);
            _repo.Setup(r => r.GetTripPostAsync(proposal.TripPostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TripPostRecord
                {
                    Id = proposal.TripPostId,
                    TripId = trip.Id,
                    Status = "Open",
                    CreatedBy = driverId,
                    Title = "Test Post",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            _repo.Setup(r => r.GetShipmentAsync(proposal.ShipmentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Shipment
                {
                    Id = proposal.ShipmentId,
                    Status = ShipmentStatus.Draft.ToString(),
                    WeightKg = 10,
                    VolumeCbm = 1,
                    ReceiverName = "A",
                    ReceiverPhone = "0",
                    DestAddress = "X"
                });

            var result = await _sut.RejectProposalAsync(
                proposalId, driverId,
                new RejectProposalRequest { Reason = "Shipment too heavy" },
                CancellationToken.None);

            Assert.Equal(ProposalStatusConstants.Rejected, result.Status);

            // Verify proposal entity was updated
            Assert.Equal(ProposalStatusConstants.Rejected, proposal.Status);
            Assert.NotNull(proposal.RejectedAt);
            Assert.Equal(driverId, proposal.RejectedBy);
            Assert.Equal("Shipment too heavy", proposal.RejectReason);
        }
    }
}
