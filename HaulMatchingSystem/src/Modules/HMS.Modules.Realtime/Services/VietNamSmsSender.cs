using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HMS.Modules.Realtime.Services
{
    public class VietNamSmsSender : ISmsSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VietNamSmsSender> _logger;
        private readonly HttpClient _httpClient;

        public VietNamSmsSender(IConfiguration configuration, ILogger<VietNamSmsSender> logger, HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            // 1. Đọc cấu hình
            var apiKey = _configuration["SmsProvider:ApiKey"] ?? string.Empty;
            var secretKey = _configuration["SmsProvider:SecretKey"] ?? string.Empty;
            var brandname = _configuration["SmsProvider:Brandname"] ?? string.Empty;

            // Ép kiểu SmsType, nếu không có cấu hình thì lấy mặc định là 2 (CSKH)
            int.TryParse(_configuration["SmsProvider:SmsType"], out int smsType);
            if (smsType == 0) smsType = 2;

            // 2. Chặn luồng nếu là Key giả (Chế độ mô phỏng)
            bool isMockKey = string.IsNullOrEmpty(apiKey)
                             || apiKey.StartsWith("YOUR_")
                             || string.IsNullOrEmpty(secretKey);

            if (isMockKey)
            {
                _logger.LogWarning($"[VN SMS SIMULATION] Đã định tuyến tin nhắn tới {phoneNumber}: {message} (Đang dùng Key giả lập)");
                return;
            }

            // 3. Gọi API thật của eSMS
            try
            {
                _logger.LogInformation($"[VN SMS LIVE] Đang gửi tin nhắn qua eSMS tới {phoneNumber}...");

                var url = "http://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post/";

                // Tạo payload chuẩn JSON của eSMS
                var payload = new
                {
                    ApiKey = apiKey,
                    SecretKey = secretKey,
                    Phone = phoneNumber,
                    Content = message,
                    SmsType = smsType,
                    Brandname = brandname
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // Gửi HTTP POST
                var response = await _httpClient.PostAsync(url, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    // eSMS trả về HTTP 200 nhưng lỗi nằm trong nội dung (CodeResult). Tạm thời ghi log thành công.
                    var responseData = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"[VN SMS LIVE] eSMS phản hồi: {responseData}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[VN SMS LIVE] Máy chủ eSMS từ chối. HTTP {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[VN SMS LIVE] Lỗi kết nối đến tổng đài SMS nội địa.");
            }
        }
    }
}
