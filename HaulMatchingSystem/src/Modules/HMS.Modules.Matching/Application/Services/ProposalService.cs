using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Application.Services
{
    /// <summary>
    /// Service implementing the new Proposal-based matching flow.
    /// 
    /// Flow:
    ///   Staff/Admin creates TripPost (existing)
    ///   Customer views TripPost marketplace â†’ creates Proposal for existing Shipment
    ///   Driver views pending Proposals â†’ Accept/Reject
    ///   
    /// Key invariants:
    ///   - A Shipment can have multiple Pending proposals for different TripPosts
    ///   - At most one Accepted proposal per Shipment at any time
    ///   - Accept triggers: Proposalâ†’Accepted, Shipmentâ†’Matched, cancel other Pending proposals
    ///   - Accept All validates total capacity before accepting any
    /// </summary>
    public class ProposalService : IProposalService
    {
        private readonly IProposalRepository _repo;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<ProposalService> _logger;

        public ProposalService(
            IProposalRepository repo,
            IShipmentStateService shipmentStateService,
            IRealtimeDispatcher dispatcher,
            ILogger<ProposalService> logger)
        {
            _repo = repo;
            _shipmentStateService = shipmentStateService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        /// <summary>
        /// Customer creates a proposal to link an existing Shipment to a TripPost.
        /// </summary>
        public async Task<CreateProposalResponse> CreateProposalAsync(
            Guid tripPostId, Guid customerId, CreateProposalRequest request, CancellationToken ct)
        {
            // 1. Validate Shipment exists and belongs to customer
            var shipment = await _repo.GetShipmentAsync(request.ShipmentId, ct)
                ?? throw new InvalidOperationException("Shipment khÃ´ng tá»“n táº¡i.");

            // 2. Validate Shipment is Draft
            if (shipment.Status != ShipmentStatus.Draft.ToString())
                throw new InvalidOperationException(
                    $"Shipment Ä‘ang á»Ÿ tráº¡ng thÃ¡i {shipment.Status}. Chá»‰ Shipment á»Ÿ tráº¡ng thÃ¡i Draft má»›i cÃ³ thá»ƒ Ä‘á» xuáº¥t.");

            // 3. Validate Weight and Volume > 0
            if (shipment.WeightKg <= 0)
                throw new InvalidOperationException("Weight cá»§a Shipment pháº£i lá»›n hÆ¡n 0.");
            if (shipment.VolumeCbm <= 0)
                throw new InvalidOperationException("Volume cá»§a Shipment pháº£i lá»›n hÆ¡n 0.");

            // 4. Validate TripPost exists and is Open
            var tripPost = await _repo.GetTripPostAsync(tripPostId, ct)
                ?? throw new InvalidOperationException("Trip Post khÃ´ng tá»“n táº¡i.");

            if (tripPost.Status != "Open")
                throw new InvalidOperationException("Trip Post khÃ´ng cÃ²n má»Ÿ. KhÃ´ng thá»ƒ táº¡o Ä‘á» xuáº¥t.");

            // 5. Check acceptUntil hasn't passed
            if (tripPost.AcceptUntil < DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Trip Post Ä‘Ã£ háº¿t háº¡n nháº­n Ä‘á» xuáº¥t.");

            // 6. Validate PickupMode is DirectPickup
            if (!string.Equals(tripPost.PickupMode, "DirectPickup", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chá»‰ há»— trá»£ Pickup Mode DirectPickup cho Ä‘á» xuáº¥t.");

            // 7. Validate required fields
            if (string.IsNullOrWhiteSpace(request.SenderName))
                throw new InvalidOperationException("Sender Name khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");
            if (string.IsNullOrWhiteSpace(request.SenderPhone))
                throw new InvalidOperationException("Sender Phone khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");
            if (string.IsNullOrWhiteSpace(request.PickupAddress))
                throw new InvalidOperationException("Pickup Address khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

            // 8. Check no duplicate Pending proposal for same Shipment + TripPost
            var existing = await _repo.GetPendingByShipmentAndTripPostAsync(request.ShipmentId, tripPostId, ct);
            if (existing != null)
                throw new InvalidOperationException(
                    "Shipment nÃ y Ä‘Ã£ cÃ³ Ä‘á» xuáº¥t Ä‘ang chá» xá»­ lÃ½ cho chuyáº¿n nÃ y.");

            // 9. Create the proposal
            var proposal = new ShipmentProposal
            {
                Id = Guid.NewGuid(),
                ShipmentId = request.ShipmentId,
                TripPostId = tripPostId,
                CustomerId = customerId,
                SenderName = request.SenderName,
                SenderPhone = request.SenderPhone,
                PickupAddress = request.PickupAddress,
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                PickupNote = request.PickupNote,
                Status = ProposalStatusConstants.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(proposal, ct);
            await _repo.SaveChangesAsync(ct);

            // 10. Notify driver via SignalR (NewShipmentProposal event)
            try
            {
                // Resolve DriverId from TripPost â†’ Trip
                var trip = await _repo.GetTripByIdAsync(tripPost.TripId, ct);
                if (trip != null)
                {
                    var pendingCount = await _repo.GetPendingByTripPostAsync(tripPostId, ct);
                    await _dispatcher.SendNewProposalToDriverAsync(trip.DriverId, new Shared.Core.Models.Realtime.ProposalEventPayload
                    {
                        EventType = "NewShipmentProposal",
                        ProposalId = proposal.Id,
                        TripPostId = tripPostId,
                        PendingProposalCount = pendingCount.Count,
                        RemainingWeightKg = 0, // Will be updated when driver views
                        RemainingVolumeCbm = 0,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification for new proposal");
            }

            _logger.LogInformation(
                "Proposal {ProposalId} created: Shipment {ShipmentId} â†’ TripPost {TripPostId} by Customer {CustomerId}",
                proposal.Id, request.ShipmentId, tripPostId, customerId);

            return new CreateProposalResponse
            {
                ProposalId = proposal.Id,
                ShipmentId = proposal.ShipmentId,
                TripPostId = proposal.TripPostId,
                Status = proposal.Status,
                CreatedAt = proposal.CreatedAt
            };
        }

        /// <summary>
        /// Customer cancels a pending proposal.
        /// </summary>
        public async Task CancelProposalAsync(Guid proposalId, Guid customerId, CancellationToken ct)
        {
            var proposal = await _repo.GetByIdAsync(proposalId, ct)
                ?? throw new InvalidOperationException("Proposal khÃ´ng tá»“n táº¡i.");

            if (proposal.CustomerId != customerId)
                throw new UnauthorizedAccessException("Báº¡n khÃ´ng cÃ³ quyá»n há»§y Ä‘á» xuáº¥t nÃ y.");

            if (proposal.Status != ProposalStatusConstants.Pending)
                throw new InvalidOperationException($"Proposal Ä‘ang á»Ÿ tráº¡ng thÃ¡i {proposal.Status}. Chá»‰ cÃ³ thá»ƒ há»§y Proposal Pending.");

            proposal.Status = ProposalStatusConstants.Cancelled;
            proposal.CancelledAt = DateTime.UtcNow;

            await _repo.UpdateAsync(proposal, ct);
            await _repo.SaveChangesAsync(ct);

            // SignalR: Notify driver that proposal was cancelled
            try
            {
                var tripPost = await _repo.GetTripPostAsync(proposal.TripPostId, ct);
                if (tripPost != null)
                {
                    var trip = await _repo.GetTripByIdAsync(tripPost.TripId, ct);
                    if (trip != null)
                    {
                        await _dispatcher.SendProposalCancelledToDriverAsync(trip.DriverId, new Shared.Core.Models.Realtime.ProposalEventPayload
                        {
                            EventType = "ShipmentProposalCancelled",
                            ProposalId = proposalId,
                            TripPostId = proposal.TripPostId,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send cancellation notification to driver for proposal {ProposalId}", proposalId);
            }

            _logger.LogInformation(
                "Proposal {ProposalId} cancelled by Customer {CustomerId}",
                proposalId, customerId);
        }

        /// <summary>
        /// Driver views all pending proposals for their active trip's trip posts.
        /// </summary>
        public async Task<DriverProposalsResponse?> GetDriverPendingProposalsAsync(Guid driverId, CancellationToken ct)
        {
            var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
            if (trip == null) return null;

            var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct);
            if (vehicle == null) return null;

            var proposals = await _repo.GetPendingByDriverAsync(driverId, ct);

            var proposalDtos = new List<ProposalDto>();
            foreach (var p in proposals)
            {
                var shipment = await _repo.GetShipmentAsync(p.ShipmentId, ct);
                if (shipment == null) continue;

                proposalDtos.Add(MapToDto(p, shipment));
            }

            return new DriverProposalsResponse
            {
                TripId = trip.Id,
                CurrentLoadWeight = trip.CurrentLoadWeight,
                CurrentLoadVolume = trip.CurrentLoadVolume,
                RemainingWeightCapacity = vehicle.MaxWeightKg - trip.CurrentLoadWeight,
                RemainingVolumeCapacity = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume,
                Proposals = proposalDtos
            };
        }

        /// <summary>
        /// Driver accepts a single proposal.
        /// Full transaction with all validations.
        /// </summary>
        public async Task<ProposalDto> AcceptProposalAsync(Guid proposalId, Guid driverId, CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                // 1. Load proposal
                var proposal = await _repo.GetByIdAsync(proposalId, ct)
                    ?? throw new InvalidOperationException("Proposal khÃ´ng tá»“n táº¡i.");

                // 2. Check proposal is Pending
                if (proposal.Status != ProposalStatusConstants.Pending)
                    throw new InvalidOperationException($"Proposal Ä‘ang á»Ÿ tráº¡ng thÃ¡i {proposal.Status}. KhÃ´ng thá»ƒ cháº¥p nháº­n.");

                // 3. Get driver's active trip
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct)
                    ?? throw new InvalidOperationException("KhÃ´ng cÃ³ chuyáº¿n Ä‘ang hoáº¡t Ä‘á»™ng.");

                // 4. Get trip post to verify it's still open and belongs to this trip
                var tripPost = await _repo.GetTripPostAsync(proposal.TripPostId, ct);
                if (tripPost == null || tripPost.Status != "Open")
                    throw new InvalidOperationException("Trip Post khÃ´ng cÃ²n mÃ¡ÅŸ.");

                if (tripPost.TripId != trip.Id)
                    throw new UnauthorizedAccessException("Proposal nÃ y khÃ´ng thuá»™c chuyáº¿n cÃ»a báº¡n.");

                // 5. Check acceptUntil
                if (tripPost.AcceptUntil < DateTimeOffset.UtcNow)
                    throw new InvalidOperationException("Trip Post Ä‘Ã£ hÃ¡Æ¡t hÃ¡n nhÃ¡ÅŸn Ä‘Ã¡Â» xuáº¥t.");

                // 6. Get shipment and verify it's Draft
                var shipment = await _repo.GetShipmentAsync(proposal.ShipmentId, ct)
                    ?? throw new InvalidOperationException("Shipment khÃ´ng tá»“n táº¡i.");

                if (shipment.Status != ShipmentStatus.Draft.ToString())
                    throw new InvalidOperationException(
                        $"Shipment Ä‘ang á»Ÿ tráº¡ng thÃ¡i {shipment.Status}. Chá»‰ Shipment Draft má»›i cÃ³ thá»ƒ cháº¥p nháº­n.");

                // 7. Check shipment doesn't already have an Accepted proposal
                if (await _repo.HasAcceptedProposalForShipmentAsync(proposal.ShipmentId, ct))
                    throw new InvalidOperationException("Shipment Ä‘Ã£ cÃ³ Ä‘á» xuáº¥t Ä‘Æ°á»£c cháº¥p nháº­n á»Ÿ chuyáº¿n khÃ¡c.");

                // 8. Check capacity
                var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct)
                    ?? throw new InvalidOperationException("Vehicle khÃ´ng tá»“n táº¡i.");

                if (trip.CurrentLoadWeight + shipment.WeightKg > vehicle.MaxWeightKg)
                    throw new InvalidOperationException("Xe khÃ´ng cÃ²n Ä‘á»§ táº£i trá»ng.");

                if (trip.CurrentLoadVolume + shipment.VolumeCbm > vehicle.MaxVolumeCbm)
                    throw new InvalidOperationException("Xe khÃ´ng cÃ²n Ä‘á»§ thá»ƒ tÃ­ch.");

                // 9. Accept the proposal
                proposal.Status = ProposalStatusConstants.Accepted;
                proposal.AcceptedAt = DateTime.UtcNow;
                proposal.AcceptedBy = driverId;
                await _repo.UpdateAsync(proposal, ct);

                // 10. Transition Shipment: Draft â†’ Matched
                var dbConn = _repo.GetUnderlyingConnection();
                var dbTxn = _repo.GetUnderlyingTransaction();

                await _shipmentStateService.TransitionAsync(
                    proposal.ShipmentId,
                    ShipmentStatus.Matched,
                    connection: dbConn,
                    transaction: dbTxn,
                    performedBy: driverId,
                    ct: ct);

                // 11. Update shipment in EF context to reflect new status
                shipment.Status = ShipmentStatus.Matched.ToString();

                // 12. Update trip capacity (optimistic concurrency via Version)
                await _repo.UpdateTripLoadAsync(
                    trip.Id, shipment.WeightKg, shipment.VolumeCbm, trip.Version, ct);

                // 13. Cancel all other Pending proposals for the same Shipment
                var otherPendingProposals = await _repo.GetPendingByShipmentAsync(proposal.ShipmentId, ct);
                foreach (var other in otherPendingProposals.Where(p => p.Id != proposalId))
                {
                    other.Status = ProposalStatusConstants.Cancelled;
                    other.CancelledAt = DateTime.UtcNow;
                    await _repo.UpdateAsync(other, ct);
                }

                await _repo.SaveChangesAsync(ct);

                // 14. SignalR: Notify capacity update to driver
                try
                {
                    var newRemainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight - shipment.WeightKg;
                    var newRemainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume - shipment.VolumeCbm;
                    await _dispatcher.SendTripCapacityUpdatedToDriverAsync(driverId, new Shared.Core.Models.Realtime.ProposalEventPayload
                    {
                        EventType = "TripCapacityUpdated",
                        ProposalId = proposalId,
                        TripPostId = proposal.TripPostId,
                        RemainingWeightKg = newRemainingWeight,
                        RemainingVolumeCbm = newRemainingVolume,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast capacity update to driver");
                }

                // 15. Notify customers whose proposals were cancelled
                foreach (var other in otherPendingProposals.Where(p => p.Id != proposalId))
                {
                    try
                    {
                        await _dispatcher.SendProposalStatusToCustomerAsync(other.CustomerId, new Shared.Core.Models.Realtime.ProposalEventPayload
                        {
                            EventType = "ShipmentProposalCancelled",
                            ProposalId = other.Id,
                            TripPostId = other.TripPostId,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send cancellation notification to customer {CustomerId}", other.CustomerId);
                    }
                }

                await _repo.CommitTransactionAsync(ct);

                _logger.LogInformation(
                    "Proposal {ProposalId} accepted by Driver {DriverId}. Shipment {ShipmentId} â†’ Matched",
                    proposalId, driverId, proposal.ShipmentId);

                var shipmentAfter = await _repo.GetShipmentAsync(proposal.ShipmentId, ct);
                return MapToDto(proposal, shipmentAfter ?? shipment);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Driver rejects a single proposal.
        /// </summary>
        public async Task<ProposalDto> RejectProposalAsync(
            Guid proposalId, Guid driverId, RejectProposalRequest request, CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                var proposal = await _repo.GetByIdAsync(proposalId, ct)
                    ?? throw new InvalidOperationException("Proposal khÃ´ng tá»“n táº¡i.");

                if (proposal.Status != ProposalStatusConstants.Pending)
                    throw new InvalidOperationException($"Proposal Ä‘ang á»Ÿ tráº¡ng thÃ¡i {proposal.Status}. KhÃ´ng thá»ƒ tá»« chá»‘i.");

                // Verify driver owns the trip
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
                if (trip == null)
                    throw new InvalidOperationException("KhÃ´ng cÃ³ chuyáº¿n Ä‘ang hoáº¡t Ä‘á»™ng.");

                // Verify the proposal is for a trip post linked to this driver's trip
                var tripPost = await _repo.GetTripPostAsync(proposal.TripPostId, ct);
                if (tripPost == null || tripPost.TripId != trip.Id)
                    throw new UnauthorizedAccessException("Proposal khÃ´ng thuá»™c chuyáº¿n cá»§a báº¡n.");

                // 1. Reject the proposal
                proposal.Status = ProposalStatusConstants.Rejected;
                proposal.RejectedAt = DateTime.UtcNow;
                proposal.RejectedBy = driverId;
                proposal.RejectReason = request.Reason;
                await _repo.UpdateAsync(proposal, ct);

                await _repo.SaveChangesAsync(ct);

                // 2. Notify customer
                try
                {
                    await _dispatcher.SendProposalStatusToCustomerAsync(proposal.CustomerId, new Shared.Core.Models.Realtime.ProposalEventPayload
                    {
                        EventType = "ProposalRejected",
                        ProposalId = proposalId,
                        TripPostId = proposal.TripPostId,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send rejection notification to customer");
                }

                await _repo.CommitTransactionAsync(ct);

                _logger.LogInformation(
                    "Proposal {ProposalId} rejected by Driver {DriverId}. Reason: {Reason}",
                    proposalId, driverId, request.Reason);

                var shipment = await _repo.GetShipmentAsync(proposal.ShipmentId, ct);
                return MapToDto(proposal, shipment!);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Driver accepts ALL pending proposals for their trip at once.
        /// All-or-nothing: if total capacity exceeds, none are accepted.
        /// </summary>
        public async Task<TripCapacityDto> AcceptAllProposalsAsync(
            Guid driverId, AcceptAllProposalsRequest request, CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct)
                    ?? throw new InvalidOperationException("KhÃ´ng cÃ³ chuyáº¿n Ä‘ang hoáº¡t Ä‘á»™ng.");

                var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct)
                    ?? throw new InvalidOperationException("Vehicle khÃ´ng tá»“n táº¡i.");

                // Get all pending proposals for this driver
                var proposals = await _repo.GetPendingByDriverAsync(driverId, ct);
                if (!proposals.Any())
                    throw new InvalidOperationException("KhÃ´ng cÃ³ Ä‘á» xuáº¥t nÃ o Ä‘á»ƒ cháº¥p nháº­n.");

                // Load all shipments and validate
                var shipmentMap = new Dictionary<Guid, Shipment>();
                decimal totalWeight = 0;
                decimal totalVolume = 0;

                foreach (var p in proposals)
                {
                    var shipment = await _repo.GetShipmentAsync(p.ShipmentId, ct);
                    if (shipment == null)
                        throw new InvalidOperationException($"Shipment {p.ShipmentId} khÃ´ng tá»“n táº¡i.");

                    if (shipment.Status != ShipmentStatus.Draft.ToString())
                        throw new InvalidOperationException(
                            $"Shipment {shipment.Id} Ä‘ang á»Ÿ tráº¡ng thÃ¡i {shipment.Status}. Chá»‰ Shipment Draft má»›i cÃ³ thá»ƒ cháº¥p nháº­n.");

                    if (shipment.WeightKg <= 0 || shipment.VolumeCbm <= 0)
                        throw new InvalidOperationException(
                            $"Shipment {shipment.Id} cÃ³ Weight hoáº·c Volume khÃ´ng há»£p lá»‡.");

                    if (await _repo.HasAcceptedProposalForShipmentAsync(shipment.Id, ct))
                        throw new InvalidOperationException(
                            $"Shipment {shipment.Id} Ä‘Ã£ cÃ³ Ä‘á» xuáº¥t Ä‘Æ°á»£c cháº¥p nháº­n á»Ÿ chuyáº¿n khÃ¡c.");

                    totalWeight += shipment.WeightKg;
                    totalVolume += shipment.VolumeCbm;
                    shipmentMap[shipment.Id] = shipment;
                }

                // Check total capacity
                if (trip.CurrentLoadWeight + totalWeight > vehicle.MaxWeightKg)
                    throw new InvalidOperationException(
                        "Xe khÃ´ng cÃ²n Ä‘á»§ táº£i trá»ng hoáº·c thá»ƒ tÃ­ch Ä‘á»ƒ nháº­n toÃ n bá»™ Ä‘á» xuáº¥t.");

                if (trip.CurrentLoadVolume + totalVolume > vehicle.MaxVolumeCbm)
                    throw new InvalidOperationException(
                        "Xe khÃ´ng cÃ²n Ä‘á»§ táº£i trá»ng hoáº·c thá»ƒ tÃ­ch Ä‘á»ƒ nháº­n toÃ n bá»™ Ä‘á» xuáº¥t.");

                var dbConn = _repo.GetUnderlyingConnection();
                var dbTxn = _repo.GetUnderlyingTransaction();

                // Track processed shipments to avoid duplicate transitions
                var processedShipments = new HashSet<Guid>();

                // Accept all proposals
                foreach (var proposal in proposals)
                {
                    var shipment = shipmentMap[proposal.ShipmentId];

                    // Accept proposal
                    proposal.Status = ProposalStatusConstants.Accepted;
                    proposal.AcceptedAt = DateTime.UtcNow;
                    proposal.AcceptedBy = driverId;
                    await _repo.UpdateAsync(proposal, ct);

                    // Transition Shipment: Draft -> Matched (only once per shipment)
                    if (processedShipments.Add(proposal.ShipmentId))
                    {
                        await _shipmentStateService.TransitionAsync(
                            proposal.ShipmentId,
                            ShipmentStatus.Matched,
                            connection: dbConn,
                            transaction: dbTxn,
                            performedBy: driverId,
                            ct: ct);

                        shipment.Status = ShipmentStatus.Matched.ToString();
                    }

                    // Cancel other Pending proposals for this shipment
                    var otherProposals = await _repo.GetPendingByShipmentAsync(proposal.ShipmentId, ct);
                    foreach (var other in otherProposals.Where(p => p.Id != proposal.Id))
                    {
                        other.Status = ProposalStatusConstants.Cancelled;
                        other.CancelledAt = DateTime.UtcNow;
                        await _repo.UpdateAsync(other, ct);
                    }
                }

                // Update trip capacity (optimistic concurrency via Version)
                await _repo.UpdateTripLoadAsync(
                    trip.Id, totalWeight, totalVolume, trip.Version, ct);

                await _repo.SaveChangesAsync(ct);

                // SignalR: Notify capacity update to driver
                try
                {
                    var newRemainingWeight = vehicle.MaxWeightKg - trip.CurrentLoadWeight - totalWeight;
                    var newRemainingVolume = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume - totalVolume;
                    await _dispatcher.SendTripCapacityUpdatedToDriverAsync(driverId, new Shared.Core.Models.Realtime.ProposalEventPayload
                    {
                        EventType = "TripCapacityUpdated",
                        ProposalId = null,
                        TripPostId = null,
                        PendingProposalCount = 0,
                        RemainingWeightKg = newRemainingWeight,
                        RemainingVolumeCbm = newRemainingVolume,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast accept-all signal to driver");
                }

                await _repo.CommitTransactionAsync(ct);

                _logger.LogInformation(
                    "AcceptAll: Driver {DriverId} accepted {Count} proposals. Trip {TripId} capacity: {Weight}/{Volume}",
                    driverId, proposals.Count, trip.Id, trip.CurrentLoadWeight, trip.CurrentLoadVolume);

                return new TripCapacityDto
                {
                    TripId = trip.Id,
                    CurrentLoadWeight = trip.CurrentLoadWeight + totalWeight,
                    CurrentLoadVolume = trip.CurrentLoadVolume + totalVolume,
                    RemainingWeightCapacity = vehicle.MaxWeightKg - trip.CurrentLoadWeight - totalWeight,
                    RemainingVolumeCapacity = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume - totalVolume
                };
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        private static ProposalDto MapToDto(ShipmentProposal proposal, Shipment shipment)
        {
            return new ProposalDto
            {
                ProposalId = proposal.Id,
                ShipmentId = proposal.ShipmentId,
                TripPostId = proposal.TripPostId,
                ShipmentCode = $"GC-{shipment.Id.ToString()[..8].ToUpper()}",
                Commodity = shipment.CargoType,
                WeightKg = shipment.WeightKg,
                VolumeCbm = shipment.VolumeCbm,
                ReceiverName = shipment.ReceiverName,
                ReceiverPhone = shipment.ReceiverPhone,
                DeliveryAddress = shipment.DestAddress,
                SenderName = proposal.SenderName,
                SenderPhone = proposal.SenderPhone,
                PickupAddress = proposal.PickupAddress,
                PickupLatitude = proposal.PickupLatitude,
                PickupLongitude = proposal.PickupLongitude,
                PickupNote = proposal.PickupNote,
                Status = proposal.Status,
                CreatedAt = proposal.CreatedAt
            };
        }
    }
}
