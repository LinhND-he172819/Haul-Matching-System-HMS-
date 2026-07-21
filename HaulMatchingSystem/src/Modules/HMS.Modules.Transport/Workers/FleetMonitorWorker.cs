using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Data;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Models.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Transport.Workers
{
    public class FleetMonitorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FleetMonitorWorker> _logger;

        public FleetMonitorWorker(IServiceProvider serviceProvider, ILogger<FleetMonitorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Fleet Monitor Worker (Signal Loss Detector) khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TransportDbContext>();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IRealtimeDispatcher>();

                    //var thresholdTime = DateTime.UtcNow.AddMinutes(-3);
                    // Demo
                    var thresholdTime = DateTime.UtcNow.AddSeconds(-20);

                    // 🔥 OPTIMIZED QUERY (NO N+1)
                    var staleTrips = await dbContext.Set<StaleTripQueryResult>()
                        .FromSqlInterpolated($@"
                    SELECT 
                        t.id AS ""TripId"",
                        g.lat AS ""Lat"",
                        g.lng AS ""Lng"",
                        g.server_received_at AS ""ServerReceivedAt""
                    FROM transport.trips t
                    LEFT JOIN LATERAL (
                        SELECT 
                            lat,
                            lng,
                            server_received_at
                        FROM transport.gps_logs g
                        WHERE g.trip_id = t.id
                        ORDER BY g.device_timestamp DESC
                        LIMIT 1
                    ) g ON true
                    WHERE t.status = 'Active'
                      AND (
                            g.server_received_at IS NULL
                            OR g.server_received_at < {thresholdTime}
                      )
                ")
                        .ToListAsync(stoppingToken);

                    // load existing exceptions in ONE query (avoid N+1)
                    var tripIds = staleTrips.Select(x => x.TripId).ToList();

                    var existingList = await dbContext.TripExceptions
                        .Where(e => tripIds.Contains(e.TripId) &&
                                    e.ExceptionType == ExceptionType.Signal_Loss.ToString())
                        .Select(e => e.TripId)
                        .ToListAsync(stoppingToken);
                    var existing = existingList.ToHashSet();

                    var newExceptions = new List<TripException>();

                    foreach (var trip in staleTrips)
                    {
                        if (existing.Contains(trip.TripId))
                            continue;

                        _logger.LogWarning($"⚠️ Mất tín hiệu GPS: Trip {trip.TripId}");

                        newExceptions.Add(new TripException
                        {
                            Id = Guid.NewGuid(),
                            TripId = trip.TripId,
                            ExceptionType = ExceptionType.Signal_Loss.ToString(),
                            Reason = "No GPS ping received for over 3 minutes.",
                            CreatedAt = DateTime.UtcNow,
                            Lat = trip.Lat,
                            Lng = trip.Lng
                        });

                        var alertPayload = new AnomalyAlertPayload
                        {
                            TripId = trip.TripId,
                            AlertType = ExceptionType.Signal_Loss,
                            Message = "Mất tín hiệu GPS quá 3 phút. Đang chờ đồng bộ thiết bị.",
                            Lat = trip.Lat ?? 0,
                            Lng = trip.Lng ?? 0,
                            DetectedAt = DateTime.UtcNow
                        };

                        await dispatcher.SendAnomalyAlertAsync(alertPayload);
                    }

                    if (newExceptions.Count > 0)
                    {
                        dbContext.TripExceptions.AddRange(newExceptions);
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét tín hiệu GPS định kỳ.");
                }

                //await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                // Demo
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
