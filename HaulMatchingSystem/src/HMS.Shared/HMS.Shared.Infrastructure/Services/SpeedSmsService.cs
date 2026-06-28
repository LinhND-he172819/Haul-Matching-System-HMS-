using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.Shared.Infrastructure.Services
{
    public class SpeedSmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SpeedSmsService> _logger;

        public SpeedSmsService(HttpClient httpClient, IConfiguration configuration, ILogger<SpeedSmsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            var token = _configuration["SpeedSms:AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{token}:x"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string content)
        {
            _logger.LogInformation($"[SpeedSMS] Bắt đầu gửi SMS đến {phoneNumber}. Nội dung: {content}");
            try
            {
                var senderId = _configuration["SpeedSms:SenderId"] ?? "SPEEDSMS"; // SPEEDSMS is default sandbox sender
                
                // 2: SMS CSKH, 3: SMS Brandname, 4: SMS OTP (if supported)
                // For test purposes without Brandname, you often use 2 or 4.
                var requestBody = new
                {
                    to = new[] { phoneNumber },
                    content = content,
                    sms_type = 4, // 4: tin nhắn gửi bằng brandname mặc định (Verify hoặc Notify)
                    sender = senderId
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);
                var stringContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.speedsms.vn/index.php/sms/send", stringContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"SpeedSMS Response: {responseString}");
                    
                    if (responseString.Contains("\"status\":\"success\"") || responseString.Contains("\"code\":\"00\""))
                    {
                        return true;
                    }
                }
                
                var errorResponse = await response.Content.ReadAsStringAsync();
                _logger.LogError($"SpeedSMS Failed: {response.StatusCode} - {errorResponse}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS via SpeedSMS");
                return false;
            }
        }
    }
}
