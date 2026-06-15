using StackExchange.Redis;

namespace HMS.Modules.Matching.Infrastructure.Redis
{
    public class RedisLockService : IRedisLockService
    {
        private readonly IConnectionMultiplexer _multiplexer;

        public RedisLockService(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
        }

        public async Task<bool> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct)
        {
            var db = _multiplexer.GetDatabase();
            return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
        }

        public async Task<bool> ReleaseLockAsync(string key, CancellationToken ct)
        {
            var db = _multiplexer.GetDatabase();
            return await db.KeyDeleteAsync(key);
        }
    }
}
