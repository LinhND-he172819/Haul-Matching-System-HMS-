using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HMS.Modules.Transport.Workers;

/// <summary>
/// Background service that expires Open trip posts whose accept_until has passed.
/// Runs every 5 minutes. Uses a simple SQL UPDATE instead of Hangfire for simplicity.
/// </summary>
public sealed class TripPostExpiryWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly ILogger<TripPostExpiryWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public TripPostExpiryWorker(IConfiguration configuration, ILogger<TripPostExpiryWorker> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TripPostExpiryWorker started. Interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                var expired = await ExpirePostsAsync(stoppingToken);
                if (expired > 0)
                {
                    _logger.LogInformation("Expired {Count} trip posts.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TripPostExpiryWorker");
                // Wait a shorter interval before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("TripPostExpiryWorker stopped.");
    }

    private async Task<int> ExpirePostsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE transport.trip_posts
            SET status = 'Expired',
                closed_at = NOW(),
                updated_at = NOW()
            WHERE status = 'Open'
              AND accept_until <= NOW()
              AND is_deleted = FALSE;
            """;
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}
