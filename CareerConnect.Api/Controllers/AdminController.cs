using CareerConnect.Application.Admin.Services;
using CareerConnect.Domain.Enums;
using CareerConnect.Application.Admin.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareerConnect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bắt buộc phải có token
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            // Chốt chặn: Chỉ Admin mới được xem thống kê
            var accountTypeClaim = User.FindFirst("account_type")?.Value;
            if (accountTypeClaim != AccountType.Admin.ToString())
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            }

            var stats = await _adminService.GetDashboardStatsAsync();
            return Ok(stats);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var accountTypeClaim = User.FindFirst("account_type")?.Value;
            if (accountTypeClaim != AccountType.Admin.ToString())
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            }

            var users = await _adminService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetAllCompanies()
        {
            var accountTypeClaim = User.FindFirst("account_type")?.Value;
            if (accountTypeClaim != AccountType.Admin.ToString())
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            }

            var companies = await _adminService.GetAllCompaniesAsync();
            return Ok(companies);
        }

        [HttpGet("packages")]
        public async Task<IActionResult> GetAllPackages()
        {
            if (!IsAdmin()) return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            return Ok(await _adminService.GetAllPackagesAsync());
        }

        [HttpPost("packages")]
        public async Task<IActionResult> CreatePackage([FromBody] SaveAdminPackageRequestDto request)
        {
            if (!IsAdmin()) return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            try { return Ok(await _adminService.CreatePackageAsync(request)); }
            catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPut("packages/{packageId:long}")]
        public async Task<IActionResult> UpdatePackage(long packageId, [FromBody] SaveAdminPackageRequestDto request)
        {
            if (!IsAdmin()) return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            try
            {
                var package = await _adminService.UpdatePackageAsync(packageId, request);
                return package is null ? NotFound(new { message = "Không tìm thấy gói dịch vụ." }) : Ok(package);
            }
            catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpDelete("packages/{packageId:long}")]
        public async Task<IActionResult> DeletePackage(long packageId)
        {
            if (!IsAdmin()) return StatusCode(403, new { message = "Bạn không có quyền truy cập." });
            var result = await _adminService.DeletePackageAsync(packageId);
            return result is null ? NotFound(new { message = "Không tìm thấy gói dịch vụ." }) : Ok(result);
        }
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            if (!TryGetAdminUserId(out var userId, out var accessError))
                return accessError!;

            var profile = await _adminService.GetAdminProfileAsync(userId);
            return profile is null
                ? NotFound(new { message = "Không tìm thấy hồ sơ quản trị viên." })
                : Ok(profile);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] CareerConnect.Application.Admin.Requests.UpdateAdminProfileRequestDto request)
        {
            if (!TryGetAdminUserId(out var userId, out var accessError))
                return accessError!;

            var profile = await _adminService.UpdateAdminProfileAsync(userId, request);
            return profile is null
                ? NotFound(new { message = "Không tìm thấy hồ sơ quản trị viên." })
                : Ok(profile);
        }

        private bool IsAdmin() => User.FindFirst("account_type")?.Value == AccountType.Admin.ToString();

        private bool TryGetAdminUserId(out Guid userId, out IActionResult? error)
        {
            userId = Guid.Empty;
            error = null;

            if (User.FindFirst("account_type")?.Value != AccountType.Admin.ToString())
            {
                error = StatusCode(403, new { message = "Bạn không có quyền truy cập." });
                return false;
            }

            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(id, out userId))
            {
                error = Unauthorized(new { message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." });
                return false;
            }

            return true;
        }
    }
}
