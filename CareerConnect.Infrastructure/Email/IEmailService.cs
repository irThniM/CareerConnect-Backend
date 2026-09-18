using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CareerConnect.Infrastructure.Email
{
    public interface IEmailService
    {
        // Hàm mới dùng Template thay cho HTML thuần
        Task SendEmailWithTemplateAsync(string toEmail, int templateId, object parameters);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

       public async Task SendEmailWithTemplateAsync(string toEmail, int templateId, object parameters)
        {
            // 1. Đọc key. Phải đảm bảo bác đặt tên đúng y xì thế này trong User Secrets
            var apiKey = _config["EmailSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception("Hệ thống không đọc được API Key. Vui lòng kiểm tra lại User Secrets.");
            }

            var payload = new
            {
                to = new[] { new { email = toEmail } },
                templateId = templateId,
                @params = parameters
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // 2. Tạo Request Message và gắn Header trực tiếp vào đây
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Headers.Add("accept", "application/json");
            request.Content = content;

            // 3. Bắn API
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi Brevo API: {error}");
            }
        }
    }
}