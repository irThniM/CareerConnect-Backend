using CareerConnect.Domain.Entities;
using CareerConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text.Json;
using UglyToad.PdfPig;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace CareerConnect.Infrastructure.Ai
{
    public interface IKybPdfService
    {
        Task<bool> AutoVerifyPdfAsync(Guid userId, IFormFile documentFile);
    }

    public class KybPdfService : IKybPdfService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config; // Thêm config để đọc API Key

        public KybPdfService(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<bool> AutoVerifyPdfAsync(Guid userId, IFormFile documentFile)
        {
            if (documentFile.ContentType != "application/pdf")
            {
                throw new Exception("Hệ thống hiện tại chỉ hỗ trợ kiểm duyệt file PDF.");
            }

            // 1. Dùng PdfPig đọc toàn bộ chữ trong file PDF
            string extractedText = "";
            using (var stream = documentFile.OpenReadStream())
            {
                using (var pdf = PdfDocument.Open(stream))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        extractedText += page.Text + " ";
                    }
                }
            }

            // 2. Dùng Regex để tìm "Mã số doanh nghiệp: 0101248141"
            var match = Regex.Match(extractedText, @"(?:Mã số doanh nghiệp|Mã số thuế|MST)[\s:\.]*([0-9]{10}(?:[0-9]{3})?)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new Exception("Không tìm thấy Mã số doanh nghiệp hợp lệ trong file PDF. Vui lòng kiểm tra lại file.");
            }

            string extractedTaxCode = match.Groups[1].Value.Trim();

            // 3. Gọi VietQR kiểm tra trạng thái và lấy thông tin Tên, Địa chỉ chuẩn
            var (isValid, apiCompanyName, apiCompanyAddress) = await GetVietQrCompanyInfo(extractedTaxCode);
            if (!isValid)
            {
                throw new Exception($"Mã số thuế {extractedTaxCode} không tồn tại hoặc đã ngừng hoạt động trên hệ thống Thuế.");
            }

            // 4. Xử lý Logic Database
            var companyMember = await _context.CompanyMembers
                .Include(cm => cm.CompanyProfile)
                .FirstOrDefaultAsync(cm => cm.UserId == userId);

            if (companyMember == null || companyMember.CompanyProfile == null)
                throw new Exception("Không tìm thấy hồ sơ doanh nghiệp.");

            var company = companyMember.CompanyProfile;

            // Kiểm tra khớp mã số thuế (nếu đã có)
            if (!string.IsNullOrEmpty(company.TaxCode) && company.TaxCode != extractedTaxCode)
            {
                throw new Exception($"Mã số thuế trên giấy phép ({extractedTaxCode}) không khớp với hệ thống ({company.TaxCode}).");
            }

            // ================== UPLOAD PDF LÊN CLOUDINARY ==================
            var account = new Account(
                _config["CloudinarySettings:CloudName"],
                _config["CloudinarySettings:ApiKey"],
                _config["CloudinarySettings:ApiSecret"]
            );
            var cloudinary = new Cloudinary(account);

            // Phải mở lại Stream mới vì Stream cũ đã bị PdfPig đọc đến cuối file rồi
            using (var stream = documentFile.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(documentFile.FileName, stream),
                    Folder = "CareerConnect/KYB_Licenses"
                };

                var uploadResult = await cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                    throw new Exception($"Lỗi upload file: {uploadResult.Error.Message}");

                // Gán link Cloudinary trả về vào Database
                company.LicensePdfUrl = uploadResult.SecureUrl.ToString();
            }
            // ===============================================================

            // 5. TỰ ĐỘNG ĐIỀN THÔNG TIN
            company.TaxCode = extractedTaxCode;

            if (!string.IsNullOrEmpty(apiCompanyName))
            {
                company.CompanyName = apiCompanyName;
            }

            if (!string.IsNullOrEmpty(apiCompanyAddress))
            {
                company.Address = apiCompanyAddress;
            }

            // 6. AUTO-APPROVE: Kích hoạt tài khoản
            company.Status = "ACTIVE";
            companyMember.Status = "ACTIVE";

            await _context.SaveChangesAsync();

            return true;
        }

        // Hàm gộp: Trả về (Trạng thái, Tên công ty, Địa chỉ)
        private async Task<(bool IsValid, string Name, string Address)> GetVietQrCompanyInfo(string taxCode)
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.vietqr.io/v2/business/{taxCode}");

            if (!response.IsSuccessStatusCode) return (false, "", "");

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var code) && code.GetString() == "00")
            {
                if (root.TryGetProperty("data", out var data))
                {
                    string status = data.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    bool isValid = status.Contains("đang hoạt động", StringComparison.OrdinalIgnoreCase);

                    // Bóc Tên và Địa chỉ
                    string name = data.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string address = data.TryGetProperty("address", out var a) ? a.GetString() ?? "" : "";

                    return (isValid, name, address);
                }
            }
            return (false, "", "");
        }
    }
}