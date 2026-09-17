using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CompanyController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("lookup-tax/{taxCode}")]
    public async Task<IActionResult> LookupTaxCode(string taxCode)
    {
        try
        {
            taxCode = taxCode.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(taxCode, @"^\d{10}(\d{3})?$"))
            {
                return BadRequest(new { message = "Mã số thuế phải gồm 10 hoặc 13 chữ số." });
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.vietqr.io/v2/business/{taxCode}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, new { message = "Lỗi kết nối máy chủ VietQR." });
            }

            // Lấy nguyên cục JSON dạng chuỗi
            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("code", out var codeEl) && codeEl.GetString() == "00")
            {
                if (root.TryGetProperty("data", out var data))
                {
                    string status = data.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "" : "";

                    // Cửa hải quan: Chặn công ty phá sản
                    if (!status.Contains("đang hoạt động", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            message = $"Mã số thuế không hợp lệ hoặc đã đóng cửa. Trạng thái: {status}"
                        });
                    }

                    // 👉 ĐIỂM ĂN TIỀN LÀ ĐÂY: Trả về y xì đúc cục JSON của VietQR
                    return Content(content, "application/json");
                }
            }

            return BadRequest(new { message = "Không tìm thấy thông tin doanh nghiệp." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi Backend: " + ex.Message });
        }
    }
}