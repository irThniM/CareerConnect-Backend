using CareerConnect.Application.Admin.Responses;
using CareerConnect.Application.Admin.Services;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            // Đếm ứng viên (AccountType = Candidate)
            var totalCandidates = await _context.Users
                .CountAsync(u => u.AccountType == AccountType.Candidate);

            // Đếm tổng công ty
            var totalCompanies = await _context.CompanyProfiles.CountAsync();

            // Đếm công ty đang chờ duyệt
            var pendingCompanies = await _context.CompanyProfiles
                .CountAsync(c => c.Status == "PENDING");

            // Tạm để 0 vì chưa làm tính năng Đăng tin (JobPost)
            var openJobs = 0;

            return new DashboardStatsDto
            {
                TotalCandidates = totalCandidates,
                TotalCompanies = totalCompanies,
                PendingCompanies = pendingCompanies,
                OpenJobs = openJobs
            };
        }

        // hàm 
        public async Task<List<UserManagementDto>> GetAllUsersAsync()
        {
            // Lấy toàn bộ User kèm theo Profile tương ứng của họ
            var users = await _context.Users
                .Include(u => u.CandidateProfile)
                .Include(u => u.AdminProfile)
                .Include(u => u.CompanyMembers)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var result = users.Select(u => new UserManagementDto
            {
                Id = u.Id,
                Email = u.Email,
                AccountType = u.AccountType.ToString(),
                Status = u.Status.ToString(),
                CreatedAt = u.CreatedAt,
                FullName = GetFullNameFromUser(u)
            }).ToList();

            return result;
        }

        // Hàm hỗ trợ để nhặt đúng cái Tên tùy theo loại tài khoản
        private string GetFullNameFromUser(User user)
        {
            if (user.AccountType == AccountType.Candidate && user.CandidateProfile != null)
                return user.CandidateProfile.FullName;

            if (user.AccountType == AccountType.Admin && user.AdminProfile != null)
                return user.AdminProfile.FullName;

            if (user.AccountType == AccountType.Employer && user.CompanyMembers.Any())
                return user.CompanyMembers.First().FullName;

            return "Chưa cập nhật";
        }

        public async Task<List<CompanyManagementDto>> GetAllCompaniesAsync()
        {
            var companies = await _context.CompanyProfiles
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return companies.Select(c => new CompanyManagementDto
            {
                Id = c.Id,
                CompanyName = c.CompanyName,
                TaxCode = c.TaxCode,
                ContactEmail = c.ContactEmail,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                LicensePdfUrl = c.LicensePdfUrl
            }).ToList();
        }
    }
}