using HMS.Modules.Matching.Application.DTOs;
using HMS.Modules.Matching.Application.Requests;
using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Infrastructure.Redis;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Matching.Application.Services
{
    public class MatchingService : IMatchingService
    {
        private readonly IMatchingRepository _repo;
        private readonly IRedisLockService _redis;
        private readonly IRealtimeDispatcher _dispatcher;
        private readonly ILogger<MatchingService> _logger;

        public MatchingService(IMatchingRepository repo, IRedisLockService redis, IRealtimeDispatcher dispatcher, ILogger<MatchingService> logger)
        {
            _repo = repo;
            _redis = redis;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task<MatchingSuggestionsResponse?> GetSuggestionsForDriverAsync(Guid driverId, CancellationToken ct)
        {
            var trip = await _repo.GetActiveTripForDriverAsync(driverId, ct);
            if (trip == null) return null;

            var vehicle = await _repo.GetVehicleAsync(trip.VehicleId, ct);
            if (vehicle == null) return null;

            var tripShipments = await _repo.GetSuggestedTripShipmentsAsync(trip.Id, ct);

            var shipmentIds = tripShipments.Select(ts => ts.ShipmentId);
            var shipments = await _repo.GetShipmentsByIdsAsync(shipmentIds, ct);

            var response = new MatchingSuggestionsResponse
            {
                TripId = trip.Id,
                CurrentLoadWeight = trip.CurrentLoadWeight,
                CurrentLoadVolume = trip.CurrentLoadVolume,
                RemainingWeightCapacity = vehicle.MaxWeightKg - trip.CurrentLoadWeight,
                RemainingVolumeCapacity = vehicle.MaxVolumeCbm - trip.CurrentLoadVolume
            };

            foreach (var ts in tripShipments)
            {
                var s = shipments.FirstOrDefault(x => x.Id == ts.ShipmentId);
                if (s == null) continue;

                response.Shipments.Add(new ShipmentSuggestionDto
                {
                    ShipmentId = s.Id,
                    ReceiverName = s.ReceiverName,
                    ReceiverPhone = s.ReceiverPhone,
                    DestinationAddress = s.DestAddress,
                    WeightKg = s.WeightKg,
                    VolumeCbm = s.VolumeCbm,
                    DeliverySequence = ts.DeliverySequence,
                    SpecialHandlingNote = s.SpecialHandlingNote
                });
            }

            return response;
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

                // update statuses
                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be accepted (already matched/in transit)");

                    s.Status = "Matched";
                }

                foreach (var ts in suggested)
                {
                    ts.Status = "Matched";
                    ts.RespondedAt = DateTime.UtcNow;
                    ts.RespondedBy = driverId;
                }

                trip.CurrentLoadWeight += totalWeight;
                trip.CurrentLoadVolume += totalVolume;

                await _repo.SaveChangesAsync(ct);

                // remove redis locks
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

                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be rejected (already matched/in transit)");

                    s.Status = "In_Warehouse";
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

                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be accepted");

                    s.Status = "Matched";
                }

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

                foreach (var s in shipments)
                {
                    if (s.Status == "Matched" || s.Status == "In_Transit")
                        throw new InvalidOperationException($"Shipment {s.Id} cannot be rejected");

                    s.Status = "In_Warehouse";
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
