using HMS.Modules.Transport.Application.DTOs;
using System.Threading.Channels;

namespace HMS.Modules.Transport.Channels
{
    public class GpsSyncChannel
    {
        private readonly Channel<List<OfflineSyncRequest>> _channel;
        public GpsSyncChannel()
        {
            // Bounded channel giúp chống cạn kiệt RAM (Out of Memory) nếu DB chết
            var options = new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _channel = Channel.CreateBounded<List<OfflineSyncRequest>>(options);
        }

        public async Task AddSyncBatchAsync(List<OfflineSyncRequest> batch, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(batch, ct);
        }

        public IAsyncEnumerable<List<OfflineSyncRequest>> ReadAllAsync(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAllAsync(ct);
        }
    }
}
