using CareerConnect.Application.Admin.Services;
using CareerConnect.Domain.Enums;
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
    }
}