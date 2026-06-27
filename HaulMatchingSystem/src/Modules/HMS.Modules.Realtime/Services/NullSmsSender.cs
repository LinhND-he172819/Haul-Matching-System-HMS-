using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HMS.Modules.Realtime.Services
{
    public class NullSmsSender : ISmsSender
    {
        private readonly ILogger<NullSmsSender> _logger;

        public NullSmsSender(ILogger<NullSmsSender> logger)
        {
            _logger = logger;
        }

        public Task SendSmsAsync(string phoneNumber, string message)
        {
            _logger.LogInformation($"[MOCK SMS] Gửi tin nhắn đến {phoneNumber}: {message}");
            return Task.CompletedTask;
        }
    }
}
