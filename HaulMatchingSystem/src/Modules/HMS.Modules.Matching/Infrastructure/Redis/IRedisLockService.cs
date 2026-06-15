namespace HMS.Modules.Matching.Infrastructure.Redis
{
    public interface IRedisLockService
    {
        Task<bool> ReleaseLockAsync(string key, CancellationToken ct);
        Task<bool> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct);
    }
}
