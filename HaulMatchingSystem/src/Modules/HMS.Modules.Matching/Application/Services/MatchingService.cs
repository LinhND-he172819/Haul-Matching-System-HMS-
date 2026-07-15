using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Requests;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Application.Services
{
    public class MatchingService : IMatchingService
    {
        private const double DefaultRouteBufferMeters = 5000;
        private const int MaxSpatialSuggestions = 20;

        private readonly IMatchingRepository _repo;
        private readonly IRedisLockService _redis;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly IShipmentStateService _shipmentStateService;
        private readonly ILogger<MatchingService> _logger;

        public MatchingService(
            IMatchingRepository repo,
            IRedisLockService redis,
            IRealtimeDispatcher dispatcher,
            IShipmentStateService shipmentStateService,
            ILogger<MatchingService> logger)
        {
            _repo = repo;
            _redis = redis;
            _dispatcher = dispatcher;
            _shipmentStateService = shipmentStateService;
            _logger = logger;
        }

        public async Task<MatchingSuggestionsResponse?> GetSuggestionsForDriverAsync(Guid driverId, CancellationToken ct)
        {
            var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
            if (trip == null) return null;

            var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct);
            if (vehicle == null) return null;

            var remainingWeightCapacity = vehicle.MaxWeightKg - trip.CurrentLoadWeight;
            var remainingVolumeCapacity = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume;
            var tripShipments = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);
            var spatialCandidatesByShipmentId = new Dictionary<Guid, SpatialShipmentCandidate>();

            if (!tripShipments.Any())
            {
                var spatialCandidates = await _repo.GetSpatialShipmentCandidatesAsync(
                    trip.Id,
                    remainingWeightCapacity,
                    remainingVolumeCapacity,
                    DefaultRouteBufferMeters,
                    MaxSpatialSuggestions,
                    ct);

                var selectedCandidates = SelectWithinRemainingCapacity(
                    spatialCandidates,
                    remainingWeightCapacity,
                    remainingVolumeCapacity);

                tripShipments = selectedCandidates
                    .Select((candidate, index) => new TripShipment
                    {
                        Id = Guid.NewGuid(),
                        TripId = trip.Id,
                        ShipmentId = candidate.Shipment.Id,
                        DeliverySequence = index + 1,
                        Status = "Suggested",
                        SuggestedAt = DateTime.UtcNow
                    })
                    .ToList();

                if (tripShipments.Any())
                {
                    await _repo.AddTripShipmentSuggestionsAsync(tripShipments, ct);
                    await _repo.SaveChangesAsync(ct);
                    spatialCandidatesByShipmentId = selectedCandidates.ToDictionary(candidate => candidate.Shipment.Id);
                }
            }

            var shipmentIds = tripShipments.Select(ts => ts.ShipmentId);
            var shipments = await _repo.GetShipmentsByIdsAsync(shipmentIds, ct);

            var response = new MatchingSuggestionsResponse
            {
                TripId = trip.Id,
                CurrentLoadWeight = trip.CurrentLoadWeight,
                CurrentLoadVolume = trip.CurrentLoadVolume,
                RemainingWeightCapacity = remainingWeightCapacity,
                RemainingVolumeCapacity = remainingVolumeCapacity
            };

            foreach (var ts in tripShipments)
            {
                var s = shipments.FirstOrDefault(x => x.Id == ts.ShipmentId);
                if (s == null) continue;

                spatialCandidatesByShipmentId.TryGetValue(s.Id, out var spatialCandidate);

                response.Shipments.Add(new ShipmentSuggestionDto
                {
                    ShipmentId = s.Id,
                    ReceiverName = s.ReceiverName,
                    ReceiverPhone = s.ReceiverPhone,
                    DestinationAddress = s.DestAddress,
                    WeightKg = s.WeightKg,
                    VolumeCbm = s.VolumeCbm,
                    DeliverySequence = ts.DeliverySequence,
                    RoutePosition = spatialCandidate?.RoutePosition,
                    RouteDistanceMeters = spatialCandidate?.DistanceMeters,
                    SpecialHandlingNote = s.SpecialHandlingNote
                });
            }

            return response;
        }

        private static List<SpatialShipmentCandidate> SelectWithinRemainingCapacity(
            IEnumerable<SpatialShipmentCandidate> candidates,
            decimal remainingWeightCapacity,
            decimal remainingVolumeCapacity)
        {
            var selected = new List<SpatialShipmentCandidate>();
            var remainingWeight = remainingWeightCapacity;
            var remainingVolume = remainingVolumeCapacity;

            foreach (var candidate in candidates.OrderBy(candidate => candidate.RoutePosition))
            {
                if (candidate.Shipment.WeightKg > remainingWeight ||
                    candidate.Shipment.VolumeCbm > remainingVolume)
                {
                    continue;
                }

                selected.Add(candidate);
                remainingWeight -= candidate.Shipment.WeightKg;
                remainingVolume -= candidate.Shipment.VolumeCbm;
            }

            return selected;
        }

        public async Task AcceptAllAsync(Guid driverId, CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
                if (trip == null) throw new InvalidOperationException("Active trip not found");

                var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct) ?? throw new InvalidOperationException("Vehicle not found");

                var suggested = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);
                var shipmentIds = suggested.Select(s => s.ShipmentId).ToList();
                var shipments = await _repo.GetShipmentsByIdsAsync(shipmentIds, ct);

                var totalWeight = shipments.Sum(s => s.WeightKg);
                var totalVolume = shipments.Sum(s => s.VolumeCbm);

                if (trip.CurrentLoadWeight + totalWeight > vehicle.MaxWeightKg)
                {
                    throw new InvalidOperationException("Weight capacity exceeded");
                }

                if (trip.CurrentLoadVolume + totalVolume > vehicle.MaxVolumeCbm)
                {
                    throw new InvalidOperationException("Volume capacity exceeded");
                }

                // ── Share the underlying Npgsql connection with ShipmentStateService ──
                var dbConn = _repo.GetUnderlyingConnection();
                var dbTxn = _repo.GetUnderlyingTransaction();

                // ── Transition shipment statuses via State Machine ──
                foreach (var s in shipments)
                {
                    await _shipmentStateService.TransitionAsync(
                        s.Id,
                        ShipmentStatus.Matched,
                        connection: dbConn,
                        transaction: dbTxn,
                        performedBy: driverId,
                        ct: ct);
                }

                // Update EF-tracked shipment entities so SaveChanges works
                foreach (var s in shipments)
                    s.Status = ShipmentStatus.Matched.ToString();

                foreach (var ts in suggested)
                {
                    ts.Status = "Matched";
                    ts.RespondedAt = DateTime.UtcNow;
                    ts.RespondedBy = driverId;
                }

                trip.CurrentLoadWeight += totalWeight;
                trip.CurrentLoadVolume += totalVolume;

                await _repo.SaveChangesAsync(ct);

                // Remove redis locks
                foreach (var sid in shipmentIds)
                {
                    var key = $"shipment:{sid}:matching-lock";
                    await _redis.ReleaseLockAsync(key, ct);
                }

                // SignalR notifications
                await _dispatcher.BroadcastMatchingAcceptedAsync(new { TripId = trip.Id, ShipmentIds = shipmentIds });

                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        public async Task RejectAllAsync(Guid driverId, CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
                if (trip == null) throw new InvalidOperationException("Active trip not found");

                var suggested = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);
                var shipmentIds = suggested.Select(s => s.ShipmentId).ToList();
                var shipments = await _repo.GetShipmentsByIdsAsync(shipmentIds, ct);

                foreach (var ts in suggested)
                {
                    ts.Status = "Rejected";
                    ts.RespondedAt = DateTime.UtcNow;
                    ts.RespondedBy = driverId;
                }

                // ── REJECT does NOT change shipment status ──
                // Shipments remain In_Warehouse. Only TripShipment.Status changes.
                // Validate that shipments are still rejectable (not already matched/in-transit).
                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be rejected (already matched/in transit)");
                }

                await _repo.SaveChangesAsync(ct);

                foreach (var sid in shipmentIds)
                {
                    var key = $"shipment:{sid}:matching-lock";
                    await _redis.ReleaseLockAsync(key, ct);
                }

                await _dispatcher.BroadcastMatchingRejectedAsync(new { TripId = trip.Id, ShipmentIds = shipmentIds });

                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        public async Task AcceptSelectedAsync(Guid driverId, AcceptSelectedRequest request, CancellationToken ct)
        {
            if (request.ShipmentIds == null || !request.ShipmentIds.Any()) return;

            await _repo.BeginTransactionAsync(ct);
            try
            {
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct) ?? throw new InvalidOperationException("Active trip not found");
                var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct) ?? throw new InvalidOperationException("Vehicle not found");

                var suggested = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);
                var toAccept = suggested.Where(ts => request.ShipmentIds.Contains(ts.ShipmentId)).ToList();
                var shipments = await _repo.GetShipmentsByIdsAsync(toAccept.Select(t => t.ShipmentId), ct);

                var totalWeight = shipments.Sum(s => s.WeightKg);
                var totalVolume = shipments.Sum(s => s.VolumeCbm);

                if (trip.CurrentLoadWeight + totalWeight > vehicle.MaxWeightKg) throw new InvalidOperationException("Weight capacity exceeded");
                if (trip.CurrentLoadVolume + totalVolume > vehicle.MaxVolumeCbm) throw new InvalidOperationException("Volume capacity exceeded");

                // ── Share the underlying Npgsql connection with ShipmentStateService ──
                var dbConn = _repo.GetUnderlyingConnection();
                var dbTxn = _repo.GetUnderlyingTransaction();

                // ── Transition shipment statuses via State Machine ──
                foreach (var s in shipments)
                {
                    await _shipmentStateService.TransitionAsync(
                        s.Id,
                        ShipmentStatus.Matched,
                        connection: dbConn,
                        transaction: dbTxn,
                        performedBy: driverId,
                        ct: ct);
                }

                // Update EF-tracked shipment entities so SaveChanges works
                foreach (var s in shipments)
                    s.Status = ShipmentStatus.Matched.ToString();

                foreach (var ts in toAccept)
                {
                    ts.Status = "Matched";
                    ts.RespondedAt = DateTime.UtcNow;
                    ts.RespondedBy = driverId;
                }

                trip.CurrentLoadWeight += totalWeight;
                trip.CurrentLoadVolume += totalVolume;

                await _repo.SaveChangesAsync(ct);

                foreach (var sid in request.ShipmentIds)
                {
                    var key = $"shipment:{sid}:matching-lock";
                    await _redis.ReleaseLockAsync(key, ct);
                }

                await _dispatcher.BroadcastMatchingAcceptedAsync(new { TripId = trip.Id, ShipmentIds = request.ShipmentIds });

                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }

        public async Task RejectSelectedAsync(Guid driverId, RejectSelectedRequest request, CancellationToken ct)
        {
            if (request.ShipmentIds == null || !request.ShipmentIds.Any()) return;

            await _repo.BeginTransactionAsync(ct);
            try
            {
                var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct) ?? throw new InvalidOperationException("Active trip not found");
                var suggested = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);
                var toReject = suggested.Where(ts => request.ShipmentIds.Contains(ts.ShipmentId)).ToList();

                var shipments = await _repo.GetShipmentsByIdsAsync(toReject.Select(t => t.ShipmentId), ct);

                foreach (var ts in toReject)
                {
                    ts.Status = "Rejected";
                    ts.RespondedAt = DateTime.UtcNow;
                    ts.RespondedBy = driverId;
                }

                // ── REJECT does NOT change shipment status ──
                // Shipments remain In_Warehouse. Only TripShipment.Status changes.
                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be rejected (already matched/in transit)");
                }

                await _repo.SaveChangesAsync(ct);

                foreach (var sid in request.ShipmentIds)
                {
                    var key = $"shipment:{sid}:matching-lock";
                    await _redis.ReleaseLockAsync(key, ct);
                }

                await _dispatcher.BroadcastMatchingRejectedAsync(new { TripId = trip.Id, ShipmentIds = request.ShipmentIds });

                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                throw;
            }
        }
    }
}
