using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CareerConnect.Infrastructure.Ai
{
    public interface IGeminiService
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // Keep public job search available even when the optional AI key is not configured.
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Thiếu cấu hình GeminiSettings:ApiKey để tạo embedding.");
            }

            // 1. Đổi sang model ổn định nhất của hệ sinh thái Gemini
            string modelName = "gemini-embedding-001";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:embedContent?key={_apiKey}";

            var requestBody = new
            {
                model = $"models/{modelName}",
                content = new
                {
                    parts = new[] { new { text = text } }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent);

            // 2. Bắt lỗi chi tiết từ Google để dễ debug nếu có biến
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"Từ chối từ Google Gemini ({response.StatusCode}): {errorDetails}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);

            var values = document.RootElement
                .GetProperty("embedding")
                .GetProperty("values")
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();

            return values;
        }
    }
}
