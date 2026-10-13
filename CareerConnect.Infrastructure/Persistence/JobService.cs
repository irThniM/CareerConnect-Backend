using CareerConnect.Domain.Entities;
using CareerConnect.Application.Employer.Exceptions;
using Microsoft.EntityFrameworkCore;
using CareerConnect.Application.Employer.Requests;
using CareerConnect.Application.Jobs.Requests;
using CareerConnect.Application.Jobs.Responses;
using CareerConnect.Application.Jobs.Services;
using CareerConnect.Application.Jobs.Exceptions;
using CareerConnect.Infrastructure.Ai; // Nạp namespace AI
using Pgvector; // Nạp kiểu Vector

namespace CareerConnect.Infrastructure.Persistence
{
    public class JobService : IJobService
    {
        private readonly AppDbContext _context;
        private readonly IGeminiService _geminiService;

        public JobService(AppDbContext context, IGeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        public async Task<string> CreateJobAsync(Guid userId, CreateJobRequestDto request)
        {
            if (!request.CategoryId.HasValue)
            {
                throw new InvalidJobCategoryException("Vui lòng chọn một danh mục tuyển dụng đang hoạt động.");
            }

            var categoryId = request.CategoryId.Value;
            var hasActiveCategory = await _context.JobCategories
                .AnyAsync(category => category.Id == categoryId && category.IsActive);
            if (!hasActiveCategory)
            {
                throw new InvalidJobCategoryException("Vui lòng chọn một danh mục tuyển dụng đang hoạt động.");
            }

            var member = await _context.CompanyMembers
                .Include(m => m.CompanyProfile)
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (member == null) throw new JobPostingNotAllowedException("Tài khoản này chưa được liên kết với doanh nghiệp.");
            if (member.CompanyProfile.Status != "ACTIVE") throw new JobPostingNotAllowedException("Công ty cần xác minh KYB trước khi đăng tin.");
            if (member.Status != "ACTIVE") throw new JobPostingNotAllowedException("Tài khoản thành viên doanh nghiệp chưa được kích hoạt.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var quotaReserved = await _context.CompanyProfiles
                    .Where(company => company.Id == member.CompanyId
                        && company.Status == "ACTIVE"
                        && company.JobPostCreditsRemaining > 0)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(company => company.JobPostCreditsRemaining, company => company.JobPostCreditsRemaining - 1)
                        .SetProperty(company => company.UpdatedAt, now));

                if (quotaReserved == 0)
                {
                    var isCompanyActive = await _context.CompanyProfiles
                        .AsNoTracking()
                        .AnyAsync(company => company.Id == member.CompanyId && company.Status == "ACTIVE");

                    if (!isCompanyActive)
                    {
                        throw new JobPostingNotAllowedException("Công ty cần xác minh KYB trước khi đăng tin.");
                    }

                    throw new JobPostCreditsExhaustedException("Công ty đã hết lượt đăng tin hiện có.");
                }

                var newJob = new Job
                {
                    CompanyId = member.CompanyId,
                    CreatedByMemberId = member.Id,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Position = request.Position,
                    // Chỉ thành viên của công ty ACTIVE mới tạo được tin, vì vậy tin được mở tuyển ngay.
                    Status = "OPEN",
                    ApplicationDeadline = request.ApplicationDeadline,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Jobs.Add(newJob);
                await _context.SaveChangesAsync();

                var newProfile = new JobProfile
                {
                    JobId = newJob.Id,
                    EmploymentType = request.EmploymentType,
                    WorkMode = request.WorkMode,
                    Quantity = request.Quantity,
                    SalaryMin = request.SalaryMin,
                    SalaryMax = request.SalaryMax,
                    IsSalaryNegotiable = request.IsSalaryNegotiable,
                    ExperienceRequired = request.ExperienceRequired,
                    EducationRequired = request.EducationRequired,
                    Location = request.Location,
                    JobDescription = request.JobDescription,
                    JobRequirements = request.JobRequirements,
                    JobBenefits = request.JobBenefits
                };
                _context.JobProfiles.Add(newProfile);
                await _context.SaveChangesAsync();

                if (request.RequiredSkillIds != null && request.RequiredSkillIds.Any())
                {
                    var skills = request.RequiredSkillIds.Select(skillId => new JobRequiredSkill
                    {
                        JobProfileId = newProfile.Id,
                        SkillId = skillId
                    });
                    _context.JobRequiredSkills.AddRange(skills);
                    await _context.SaveChangesAsync();
                }

                // ==========================================
                // PHẦN MỚI THÊM: GỌI AI NHÚNG VECTOR
                // ==========================================

                // 1. Nén thông tin cốt lõi lại thành đoạn text
                var textToEmbed = $"Vị trí: {request.Position}. " +
                                  $"Hình thức: {request.EmploymentType}, {request.WorkMode}. " +
                                  $"Yêu cầu kinh nghiệm: {request.ExperienceRequired}. " +
                                  $"Mô tả chi tiết: {request.JobDescription} " +
                                  $"Kỹ năng cần có: {request.JobRequirements}";

                newProfile.NormalizedText = textToEmbed;

                // 2. Bắn sang Gemini
                var vectorArray = await _geminiService.GetEmbeddingAsync(textToEmbed);

                // 3. Đổ dữ liệu vector vào Entity
                newProfile.Embedding = new Vector(vectorArray);
                newProfile.EmbeddingUpdatedAt = DateTime.UtcNow;

                _context.JobProfiles.Update(newProfile);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return "Đăng tin thành công! AI đã tạo xong Vector.";
            }
            catch (JobPostCreditsExhaustedException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (JobPostingNotAllowedException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IReadOnlyList<JobCategoryDto>> GetActiveJobCategoriesAsync()
        {
            return await _context.JobCategories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.Name)
                .Select(category => new JobCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug
                })
                .ToListAsync();
        }

        public async Task<JobSearchResponseDto> SearchPublicJobsAsync(JobSearchRequestDto request)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var query = PublicJobsQuery();

            var keyword = request.Keyword?.Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var pattern = $"%{keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(job =>
                    EF.Functions.ILike(job.Title, pattern, "\\") ||
                    EF.Functions.ILike(job.Position, pattern, "\\") ||
                    EF.Functions.ILike(job.Company.CompanyName, pattern, "\\") ||
                    (job.Category != null && EF.Functions.ILike(job.Category.Name, pattern, "\\")) ||
                    EF.Functions.ILike(job.JobProfile.JobDescription, pattern, "\\") ||
                    EF.Functions.ILike(job.JobProfile.JobRequirements, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                var location = request.Location.Trim();
                var pattern = $"%{location.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(job => EF.Functions.ILike(job.JobProfile.Location ?? string.Empty, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(request.EmploymentType))
            {
                var employmentType = request.EmploymentType.Trim().ToUpperInvariant();
                query = query.Where(job => job.JobProfile.EmploymentType == employmentType);
            }

            if (!string.IsNullOrWhiteSpace(request.WorkMode))
            {
                var workMode = request.WorkMode.Trim().ToUpperInvariant();
                query = query.Where(job => job.JobProfile.WorkMode == workMode);
            }

            if (!string.IsNullOrWhiteSpace(request.Industry))
            {
                var industry = request.Industry.Trim();
                var pattern = $"%{industry.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(job => EF.Functions.ILike(job.Company.Industry ?? string.Empty, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(request.Experience))
            {
                var experience = request.Experience.Trim();
                var normalizedExperience = experience.Replace(" ", string.Empty);
                var pattern = $"%{normalizedExperience.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(job => EF.Functions.ILike(
                    (job.JobProfile.ExperienceRequired ?? string.Empty).Replace(" ", string.Empty),
                    pattern,
                    "\\"));
            }

            if (request.SalaryMin.HasValue || request.SalaryMax.HasValue)
            {
                var minimumSalary = request.SalaryMin;
                var maximumSalary = request.SalaryMax;

                query = query.Where(job =>
                    !job.JobProfile.IsSalaryNegotiable &&
                    (job.JobProfile.SalaryMin.HasValue || job.JobProfile.SalaryMax.HasValue) &&
                    (!minimumSalary.HasValue || !job.JobProfile.SalaryMax.HasValue || job.JobProfile.SalaryMax >= minimumSalary.Value) &&
                    (!maximumSalary.HasValue || !job.JobProfile.SalaryMin.HasValue || job.JobProfile.SalaryMin <= maximumSalary.Value));
            }

            if (request.CategoryId.HasValue)
            {
                query = query.Where(job => job.CategoryId == request.CategoryId.Value);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(job => new JobListItemDto
                {
                    Id = job.Id,
                    Title = job.Title,
                    Position = job.Position,
                    CompanyName = job.Company.CompanyName,
                    Industry = job.Company.Industry,
                    CategoryName = job.Category == null ? null : job.Category.Name,
                    Location = job.JobProfile.Location,
                    SalaryMin = job.JobProfile.SalaryMin,
                    SalaryMax = job.JobProfile.SalaryMax,
                    IsSalaryNegotiable = job.JobProfile.IsSalaryNegotiable,
                    EmploymentType = job.JobProfile.EmploymentType,
                    WorkMode = job.JobProfile.WorkMode,
                    ExperienceRequired = job.JobProfile.ExperienceRequired,
                    Quantity = job.JobProfile.Quantity,
                    ApplicationDeadline = job.ApplicationDeadline,
                    CreatedAt = job.CreatedAt,
                    RequiredSkills = job.JobProfile.RequiredSkills
                        .Select(requiredSkill => requiredSkill.Skill.Name)
                        .OrderBy(name => name)
                        .ToList()
                })
                .ToListAsync();

            return new JobSearchResponseDto
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<JobDetailsDto?> GetPublicJobDetailsAsync(long jobId)
        {
            return await PublicJobsQuery()
                .Where(job => job.Id == jobId)
                .Select(job => new JobDetailsDto
                {
                    Id = job.Id,
                    Title = job.Title,
                    Position = job.Position,
                    CategoryName = job.Category == null ? null : job.Category.Name,
                    Location = job.JobProfile.Location,
                    SalaryMin = job.JobProfile.SalaryMin,
                    SalaryMax = job.JobProfile.SalaryMax,
                    IsSalaryNegotiable = job.JobProfile.IsSalaryNegotiable,
                    EmploymentType = job.JobProfile.EmploymentType,
                    WorkMode = job.JobProfile.WorkMode,
                    ExperienceRequired = job.JobProfile.ExperienceRequired,
                    EducationRequired = job.JobProfile.EducationRequired,
                    Quantity = job.JobProfile.Quantity,
                    ApplicationDeadline = job.ApplicationDeadline,
                    CreatedAt = job.CreatedAt,
                    JobDescription = job.JobProfile.JobDescription,
                    JobRequirements = job.JobProfile.JobRequirements,
                    JobBenefits = job.JobProfile.JobBenefits,
                    RequiredSkills = job.JobProfile.RequiredSkills
                        .Select(requiredSkill => requiredSkill.Skill.Name)
                        .OrderBy(name => name)
                        .ToList(),
                    Company = new JobCompanyDto
                    {
                        Name = job.Company.CompanyName,
                        Industry = job.Company.Industry,
                        CompanySize = job.Company.CompanySize,
                        Website = job.Company.Website,
                        Description = job.Company.Description,
                        Address = job.Company.Address
                    }
                })
                .SingleOrDefaultAsync();
        }

        private IQueryable<Job> PublicJobsQuery()
        {
            var today = DateTime.UtcNow.Date;
            return _context.Jobs
                .AsNoTracking()
                .Where(job => job.Status == "OPEN")
                .Where(job => job.Company.Status == "ACTIVE")
                .Where(job => !job.ApplicationDeadline.HasValue || job.ApplicationDeadline.Value >= today);
        }
    }
}
