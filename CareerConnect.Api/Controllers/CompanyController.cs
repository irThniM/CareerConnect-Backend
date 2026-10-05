using CareerConnect.Infrastructure.Ai; // Khai báo đường dẫn tới thư mục Ai
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http; // Thêm thư viện để nhận file upload
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CareerConnect.Api.Controllers // Gói vào namespace cho chuẩn
{
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

                var content = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("code", out var codeEl) && codeEl.GetString() == "00")
                {
                    if (root.TryGetProperty("data", out var data))
                    {
                        string status = data.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "" : "";

                        if (!status.Contains("đang hoạt động", StringComparison.OrdinalIgnoreCase))
                        {
                            return BadRequest(new
                            {
                                message = $"Mã số thuế không hợp lệ hoặc đã đóng cửa. Trạng thái: {status}"
                            });
                        }

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

        [HttpPost("verify-kyb")]
        [Authorize] // Bắt buộc user phải gửi Token hợp lệ
        public async Task<IActionResult> VerifyCompanyKYB([FromServices] IKybPdfService kybPdfService, IFormFile document)
        {
            if (document == null || document.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng đính kèm tài liệu." });
            }

            try
            {
                // 1. Tự động móc ID từ Token của user đang thực hiện request
                // Nó sẽ tự dò tìm các key phổ biến thường dùng khi tạo JWT
                string userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? User.FindFirstValue("sub")
                                   ?? User.FindFirstValue("id")
                                   ?? User.FindFirstValue("UserId");

                if (!Guid.TryParse(userIdString, out Guid currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định danh tính tài khoản. Vui lòng đăng nhập lại." });
                }

                // 2. Chạy Service duyệt file với ID chuẩn
                bool result = await kybPdfService.AutoVerifyPdfAsync(currentUserId, document);

                if (result)
                {
                    return Ok(new { message = "Hệ thống đã xác minh tài liệu thành công. Tài khoản doanh nghiệp đã được KÍCH HOẠT!" });
                }

                return BadRequest(new { message = "Tài liệu không hợp lệ." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-profile")]
        [Authorize]
        public async Task<IActionResult> GetMyCompanyProfile([FromServices] CareerConnect.Infrastructure.Persistence.AppDbContext context)
        {
            try
            {
                // 1. Móc UserId từ Token
                string userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? User.FindFirstValue("sub")
                                   ?? User.FindFirstValue("id")
                                   ?? User.FindFirstValue("UserId");

                if (!Guid.TryParse(userIdString, out Guid currentUserId))
                {
                    return Unauthorized(new { message = "Không thể xác định danh tính tài khoản." });
                }

                // 2. Tra Database tìm CompanyProfile liên kết với User này
                var companyMember = await context.CompanyMembers
                    .Include(cm => cm.CompanyProfile)
                    .FirstOrDefaultAsync(cm => cm.UserId == currentUserId);

                if (companyMember == null || companyMember.CompanyProfile == null)
                {
                    return NotFound(new { message = "Bạn chưa có hồ sơ doanh nghiệp." });
                }

                var company = companyMember.CompanyProfile;

                // 3. Trả về đúng các trường mà Frontend đang cần đọc
                return Ok(new
                {
                    status = company.Status,
                    licensePdfUrl = company.LicensePdfUrl,
                    companyName = company.CompanyName,
                    taxCode = company.TaxCode,
                    address = company.Address
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}