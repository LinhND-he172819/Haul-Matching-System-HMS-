using HMS.Modules.Matching.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace HMS.Modules.Matching.Infrastructure
{
    public class MatchingRepository : IMatchingRepository
    {
        private readonly MatchingDbContext _db;
        private IDbContextTransaction? _tx;

        public MatchingRepository(MatchingDbContext db)
        {
            _db = db;
        }

        public async Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct)
        {
            return await _db.Trips.FirstOrDefaultAsync(t => t.DriverId == driverId && t.Status == "Active", ct);
        }

        public async Task<List<TripShipment>> GetSuggestedTripShipmentsAsync(Guid tripId, CancellationToken ct)
        {
            return await _db.TripShipments
                .Where(ts => ts.TripId == tripId && ts.Status == "Suggested")
                .OrderBy(ts => ts.DeliverySequence)
                .ToListAsync(ct);
        }

        public async Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct)
        {
            return await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        }

        public async Task<List<Shipment>> GetShipmentsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            return await _db.Shipments.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        }

        public async Task<List<SpatialShipmentCandidate>> GetSpatialShipmentCandidatesAsync(
            Guid tripId,
            decimal remainingWeightCapacity,
            decimal remainingVolumeCapacity,
            double routeBufferMeters,
            int limit,
            CancellationToken ct)
        {
            var candidates = new List<SpatialShipmentCandidate>();
            var connection = _db.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync(ct);
            }

            try
            {
                await using var command = connection.CreateCommand();
                if (_tx is not null)
                {
                    command.Transaction = _tx.GetDbTransaction();
                }

                command.CommandText = """
                    SELECT
                        s.id,
                        s.receiver_name,
                        s.receiver_phone,
                        s.dest_address,
                        s.weight_kg,
                        s.volume_cbm,
                        s.cargo_type,
                        s.special_handling_note,
                        s.status,
                        ST_LineLocatePoint(tt.route_linestring, s.dest_location::geometry) AS route_position,
                        ST_Distance(tt.route_linestring::geography, s.dest_location) AS distance_meters
                    FROM warehouse.shipments s
                    JOIN transport.trips tt ON tt.id = @trip_id
                    WHERE tt.is_deleted = FALSE
                        AND s.dest_location IS NOT NULL
                        AND s.status = 'In_Warehouse'
                        AND s.weight_kg <= @remaining_weight_capacity
                        AND s.volume_cbm <= @remaining_volume_capacity
                        AND ST_DWithin(
                            tt.route_linestring::geography,
                            s.dest_location,
                            @route_buffer_meters)
                        AND NOT EXISTS (
                            SELECT 1
                            FROM transport.trip_shipments ts
                            WHERE ts.shipment_id = s.id
                                AND ts.status IN ('Suggested', 'Matched')
                        )
                    ORDER BY route_position ASC, distance_meters ASC
                    LIMIT @limit;
                    """;

                command.Parameters.Add(new NpgsqlParameter("trip_id", NpgsqlDbType.Uuid) { Value = tripId });
                command.Parameters.Add(new NpgsqlParameter("remaining_weight_capacity", NpgsqlDbType.Numeric) { Value = remainingWeightCapacity });
                command.Parameters.Add(new NpgsqlParameter("remaining_volume_capacity", NpgsqlDbType.Numeric) { Value = remainingVolumeCapacity });
                command.Parameters.Add(new NpgsqlParameter("route_buffer_meters", NpgsqlDbType.Double) { Value = routeBufferMeters });
                command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = limit });

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    candidates.Add(new SpatialShipmentCandidate
                    {
                        Shipment = new Shipment
                        {
                            Id = reader.GetGuid(reader.GetOrdinal("id")),
                            ReceiverName = ReadNullableString(reader, "receiver_name"),
                            ReceiverPhone = ReadNullableString(reader, "receiver_phone"),
                            DestAddress = ReadNullableString(reader, "dest_address"),
                            WeightKg = reader.GetDecimal(reader.GetOrdinal("weight_kg")),
                            VolumeCbm = reader.GetDecimal(reader.GetOrdinal("volume_cbm")),
                            CargoType = ReadNullableString(reader, "cargo_type"),
                            SpecialHandlingNote = ReadNullableString(reader, "special_handling_note"),
                            Status = ReadNullableString(reader, "status")
                        },
                        RoutePosition = reader.GetDouble(reader.GetOrdinal("route_position")),
                        DistanceMeters = reader.GetDouble(reader.GetOrdinal("distance_meters"))
                    });
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            return candidates;
        }

        public async Task AddTripShipmentSuggestionsAsync(IEnumerable<TripShipment> suggestions, CancellationToken ct)
        {
            await _db.TripShipments.AddRangeAsync(suggestions, ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            await _db.SaveChangesAsync(ct);
        }

        public Task BeginTransactionAsync(CancellationToken ct)
        {
            if (_db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                _tx = null;
                return Task.CompletedTask;
            }

            return BeginTransactionCoreAsync(ct);
        }

        private async Task BeginTransactionCoreAsync(CancellationToken ct)
        {
            _tx = await _db.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct)
        {
            if (_tx is not null)
            {
                await _tx.CommitAsync(ct);
                await _tx.DisposeAsync();
                _tx = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct)
        {
            if (_tx is not null)
            {
                await _tx.RollbackAsync(ct);
                await _tx.DisposeAsync();
                _tx = null;
            }
        }

        private static string? ReadNullableString(System.Data.Common.DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}
