using CareerConnect.Application.Admin.Responses;
using CareerConnect.Application.Admin.Services;
using CareerConnect.Application.Admin.Requests;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private const decimal MinimumPackagePrice = 5_000m;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AdminPackageDto>> GetAllPackagesAsync()
        {
            var packages = await _context.Packages.AsNoTracking()
                .OrderByDescending(package => package.IsActive)
                .ThenBy(package => package.Price)
                .ThenBy(package => package.Id)
                .ToListAsync();
            return packages.Select(ToAdminPackageDto).ToList();
        }

        public async Task<AdminPackageDto> CreatePackageAsync(SaveAdminPackageRequestDto request)
        {
            ValidatePackage(request);
            var package = new Package
            {
                Name = request.Name.Trim(),
                Description = CleanDescription(request.Description),
                Price = request.Price,
                DurationDays = request.DurationDays,
                MemberLimit = request.MemberLimit,
                JobPostLimit = request.JobPostLimit,
                IsActive = request.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return ToAdminPackageDto(package);
        }

        public async Task<AdminPackageDto?> UpdatePackageAsync(long packageId, SaveAdminPackageRequestDto request)
        {
            ValidatePackage(request);
            var package = await _context.Packages.FirstOrDefaultAsync(item => item.Id == packageId);
            if (package is null) return null;

            var description = CleanDescription(request.Description);
            var hasSubscriptions = await _context.CompanySubscriptions.AnyAsync(subscription => subscription.PackageId == packageId);
            var changesPurchasedTerms = package.Name != request.Name.Trim()
                || package.Description != description
                || package.Price != request.Price
                || package.DurationDays != request.DurationDays
                || package.MemberLimit != request.MemberLimit
                || package.JobPostLimit != request.JobPostLimit;
            if (hasSubscriptions && changesPurchasedTerms)
                throw new ArgumentException("Gói đã có giao dịch nên không thể đổi giá hoặc quyền lợi. Hãy tạo gói mới và ngừng bán gói cũ để giữ đúng lịch sử.");
            package.Name = request.Name.Trim();
            package.Description = description;
            package.Price = request.Price;
            package.DurationDays = request.DurationDays;
            package.MemberLimit = request.MemberLimit;
            package.JobPostLimit = request.JobPostLimit;
            package.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return ToAdminPackageDto(package);
        }

        public async Task<AdminPackageDeleteResultDto?> DeletePackageAsync(long packageId)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(item => item.Id == packageId);
            if (package is null) return null;

            var isReferenced = await _context.CompanySubscriptions
                .AnyAsync(subscription => subscription.PackageId == packageId);
            if (isReferenced)
            {
                package.IsActive = false;
                await _context.SaveChangesAsync();
                return new AdminPackageDeleteResultDto
                {
                    Deleted = false,
                    IsActive = false,
                    Message = "Gói đã có giao dịch liên quan nên được ngừng bán để giữ nguyên lịch sử."
                };
            }

            _context.Packages.Remove(package);
            await _context.SaveChangesAsync();
            return new AdminPackageDeleteResultDto
            {
                Deleted = true,
                IsActive = false,
                Message = "Đã xóa gói dịch vụ."
            };
        }

        private static void ValidatePackage(SaveAdminPackageRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                throw new ArgumentException("Tên gói là bắt buộc và tối đa 120 ký tự.");
            if (request.Description?.Trim().Length > 1000)
                throw new ArgumentException("Mô tả tối đa 1.000 ký tự.");
            if (request.Price < MinimumPackagePrice || request.Price != decimal.Truncate(request.Price))
                throw new ArgumentException("Giá gói phải là số nguyên từ 5.000 VND để tương thích VNPay Sandbox.");
            if (request.DurationDays is < 1 or > 3650)
                throw new ArgumentException("Thời hạn gói phải từ 1 đến 3.650 ngày.");
            if (request.MemberLimit is <= 0 || request.JobPostLimit is <= 0)
                throw new ArgumentException("Giới hạn thành viên và lượt đăng phải lớn hơn 0 hoặc để trống nếu không giới hạn.");
        }

        private static string? CleanDescription(string? description) =>
            string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        private static AdminPackageDto ToAdminPackageDto(Package package) => new()
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            DurationDays = package.DurationDays,
            MemberLimit = package.MemberLimit,
            JobPostLimit = package.JobPostLimit,
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt
        };

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

            // Chỉ tính các tin đang mở và thuộc doanh nghiệp đã xác minh.
            var openJobs = await _context.Jobs
                .CountAsync(job => job.Status == "OPEN" && job.Company.Status == "ACTIVE");

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

        public async Task<AdminProfileDto?> GetAdminProfileAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(account => account.AdminProfile)
                .FirstOrDefaultAsync(account => account.Id == userId && account.AccountType == AccountType.Admin);

            if (user?.AdminProfile is not { } profile)
                return null;

            return ToAdminProfileDto(user, profile);
        }

        public async Task<AdminProfileDto?> UpdateAdminProfileAsync(Guid userId, CareerConnect.Application.Admin.Requests.UpdateAdminProfileRequestDto request)
        {
            var user = await _context.Users
                .Include(account => account.AdminProfile)
                .FirstOrDefaultAsync(account => account.Id == userId && account.AccountType == AccountType.Admin);

            if (user?.AdminProfile is not { } profile)
                return null;

            profile.FullName = request.FullName.Trim();
            profile.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim();
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
            return ToAdminProfileDto(user, profile);
        }

        private static AdminProfileDto ToAdminProfileDto(User user, AdminProfile profile) => new()
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = profile.FullName,
            PhoneNumber = profile.PhoneNumber,
            AvatarUrl = profile.AvatarUrl,
            RoleCode = profile.RoleCode,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }
}
