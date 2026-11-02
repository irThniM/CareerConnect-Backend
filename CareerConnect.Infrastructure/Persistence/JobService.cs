using CareerConnect.Domain.Entities;
using CareerConnect.Application.Employer.Exceptions;
using Microsoft.EntityFrameworkCore;
using CareerConnect.Application.Employer.Requests;
using CareerConnect.Application.Employer.Responses;
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
                var paidQuotaReserved = await ReserveActiveSubscriptionQuotaAsync(member.CompanyId, now);
                var quotaReserved = paidQuotaReserved
                    ? 1
                    : await _context.CompanyProfiles
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

                var textToEmbed = await BuildJobEmbeddingInputAsync(request);

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

        public async Task<CreateJobRequestDto?> GetEmployerJobForEditAsync(
            Guid userId,
            long jobId,
            CancellationToken cancellationToken = default)
        {
            var job = await _context.Jobs
                .AsNoTracking()
                .Include(item => item.JobProfile)
                    .ThenInclude(profile => profile.RequiredSkills)
                .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);

            if (job is null) return null;
            await EnsureEmployerOwnsJobAsync(userId, job.CompanyId, cancellationToken);

            var profile = job.JobProfile;
            return new CreateJobRequestDto
            {
                Title = job.Title,
                Position = job.Position,
                CategoryId = job.CategoryId,
                ApplicationDeadline = job.ApplicationDeadline,
                EmploymentType = profile.EmploymentType,
                WorkMode = profile.WorkMode,
                Quantity = profile.Quantity,
                SalaryMin = profile.SalaryMin,
                SalaryMax = profile.SalaryMax,
                IsSalaryNegotiable = profile.IsSalaryNegotiable,
                ExperienceRequired = profile.ExperienceRequired,
                EducationRequired = profile.EducationRequired,
                Location = profile.Location,
                JobDescription = profile.JobDescription,
                JobRequirements = profile.JobRequirements,
                JobBenefits = profile.JobBenefits,
                RequiredSkillIds = profile.RequiredSkills.Select(skill => skill.SkillId).ToList()
            };
        }

        public async Task<bool> UpdateJobAsync(
            Guid userId,
            long jobId,
            CreateJobRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!request.CategoryId.HasValue || !await _context.JobCategories.AnyAsync(
                    category => category.Id == request.CategoryId.Value && category.IsActive,
                    cancellationToken))
            {
                throw new InvalidJobCategoryException("Vui lòng chọn một danh mục tuyển dụng đang hoạt động.");
            }

            var job = await _context.Jobs
                .Include(item => item.JobProfile)
                    .ThenInclude(profile => profile.RequiredSkills)
                .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
            if (job is null) return false;
            await EnsureEmployerOwnsJobAsync(userId, job.CompanyId, cancellationToken);

            var skillIds = (request.RequiredSkillIds ?? new List<long>()).Distinct().ToList();
            if (skillIds.Count > 0)
            {
                var validSkillIds = await _context.Skills
                    .Where(skill => skill.IsActive && skillIds.Contains(skill.Id))
                    .Select(skill => skill.Id)
                    .ToListAsync(cancellationToken);
                if (validSkillIds.Count != skillIds.Count)
                {
                    throw new InvalidJobSkillException("Một hoặc nhiều kỹ năng đã chọn không còn khả dụng.");
                }
            }

            var existingProfile = job.JobProfile;
            var existingEmbeddingInput = await BuildJobEmbeddingInputAsync(new CreateJobRequestDto
            {
                Title = job.Title,
                Position = job.Position,
                CategoryId = job.CategoryId,
                ApplicationDeadline = job.ApplicationDeadline,
                EmploymentType = existingProfile.EmploymentType,
                WorkMode = existingProfile.WorkMode,
                Quantity = existingProfile.Quantity,
                SalaryMin = existingProfile.SalaryMin,
                SalaryMax = existingProfile.SalaryMax,
                IsSalaryNegotiable = existingProfile.IsSalaryNegotiable,
                ExperienceRequired = existingProfile.ExperienceRequired,
                EducationRequired = existingProfile.EducationRequired,
                Location = existingProfile.Location,
                JobDescription = existingProfile.JobDescription,
                JobRequirements = existingProfile.JobRequirements,
                JobBenefits = existingProfile.JobBenefits,
                RequiredSkillIds = existingProfile.RequiredSkills.Select(skill => skill.SkillId).ToList()
            });
            var updatedEmbeddingInput = await BuildJobEmbeddingInputAsync(request);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var profile = job.JobProfile;
                job.Title = request.Title.Trim();
                job.Position = request.Position.Trim();
                job.CategoryId = request.CategoryId;
                job.ApplicationDeadline = request.ApplicationDeadline;
                job.UpdatedAt = DateTime.UtcNow;

                profile.EmploymentType = request.EmploymentType;
                profile.WorkMode = request.WorkMode;
                profile.Quantity = request.Quantity;
                profile.SalaryMin = request.SalaryMin;
                profile.SalaryMax = request.SalaryMax;
                profile.IsSalaryNegotiable = request.IsSalaryNegotiable;
                profile.ExperienceRequired = request.ExperienceRequired;
                profile.EducationRequired = request.EducationRequired;
                profile.Location = request.Location?.Trim();
                profile.JobDescription = request.JobDescription.Trim();
                profile.JobRequirements = request.JobRequirements.Trim();
                profile.JobBenefits = request.JobBenefits?.Trim();

                _context.JobRequiredSkills.RemoveRange(profile.RequiredSkills);
                profile.RequiredSkills.Clear();
                foreach (var skillId in skillIds)
                {
                    profile.RequiredSkills.Add(new JobRequiredSkill { SkillId = skillId });
                }

                if (profile.Embedding is null || !string.Equals(existingEmbeddingInput, updatedEmbeddingInput, StringComparison.Ordinal))
                {
                    var vector = await _geminiService.GetEmbeddingAsync(updatedEmbeddingInput, cancellationToken);
                    profile.Embedding = new Vector(vector);
                    profile.EmbeddingUpdatedAt = DateTime.UtcNow;
                }
                profile.NormalizedText = updatedEmbeddingInput;
                job.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> ExtendJobDeadlineAsync(
            Guid userId,
            long jobId,
            DateTime applicationDeadline,
            CancellationToken cancellationToken = default)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
            if (job is null) return false;
            await EnsureEmployerOwnsJobAsync(userId, job.CompanyId, cancellationToken);

            if (job.Status != "OPEN")
            {
                throw new ArgumentException("Chỉ có thể gia hạn tin đang tuyển.");
            }

            var newDeadline = DateTime.SpecifyKind(applicationDeadline.Date, DateTimeKind.Utc);
            var today = DateTime.UtcNow.Date;
            if (newDeadline <= today || (job.ApplicationDeadline.HasValue && newDeadline <= job.ApplicationDeadline.Value.Date))
            {
                throw new ArgumentException("Hạn mới phải sau ngày hôm nay và sau hạn hiện tại.");
            }

            job.ApplicationDeadline = newDeadline;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<string> BuildJobEmbeddingInputAsync(CreateJobRequestDto request)
        {
            var skillIds = (request.RequiredSkillIds ?? new List<long>()).Distinct().ToList();
            var skillNames = skillIds.Count == 0
                ? new List<string>()
                : await _context.Skills.AsNoTracking()
                    .Where(skill => skillIds.Contains(skill.Id) && skill.IsActive)
                    .OrderBy(skill => skill.Name)
                    .Select(skill => skill.Name)
                    .ToListAsync();

            return $"Tin tuyển dụng: {request.Title}. Vị trí: {request.Position}. " +
                   $"Địa điểm làm việc: {request.Location}. " +
                   $"Hình thức: {request.EmploymentType}, {request.WorkMode}. " +
                   $"Yêu cầu kinh nghiệm: {request.ExperienceRequired}. " +
                   $"Mô tả chi tiết: {request.JobDescription} " +
                   $"Yêu cầu ứng viên: {request.JobRequirements}. " +
                   $"Kỹ năng yêu cầu: {string.Join(", ", skillNames)}. " +
                   $"Quyền lợi: {request.JobBenefits}";
        }

        private async Task EnsureEmployerOwnsJobAsync(
            Guid userId,
            long companyId,
            CancellationToken cancellationToken)
        {
            var isMember = await _context.CompanyMembers.AnyAsync(
                member => member.UserId == userId
                    && member.CompanyId == companyId
                    && member.Status == "ACTIVE",
                cancellationToken);
            if (!isMember)
            {
                throw new JobPostingNotAllowedException("Bạn không có quyền quản lý tin tuyển dụng này.");
            }
        }

        public async Task<EmployerDashboardDto?> GetEmployerDashboardAsync(Guid userId)
        {
            var company = await _context.CompanyMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId)
                .Select(member => new
                {
                    member.CompanyId,
                    member.CompanyProfile.CompanyName,
                    member.CompanyProfile.Status,
                    member.CompanyProfile.JobPostCreditsRemaining
                })
                .FirstOrDefaultAsync();

            if (company is null) return null;

            var now = DateTimeOffset.UtcNow;
            var activePaidQuotas = await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(subscription => subscription.CompanyId == company.CompanyId
                    && subscription.Status == "ACTIVE"
                    && subscription.StartAt <= now
                    && subscription.EndAt > now)
                .Select(subscription => subscription.RemainingJobPosts)
                .ToListAsync();
            var hasUnlimitedJobPosts = activePaidQuotas.Any(quota => quota is null);
            var availablePaidPosts = activePaidQuotas.Where(quota => quota.HasValue).Sum(quota => quota!.Value);

            var jobs = _context.Jobs.AsNoTracking().Where(job => job.CompanyId == company.CompanyId);
            var summary = await jobs
                .GroupBy(job => job.CompanyId)
                .Select(group => new
                {
                    TotalJobs = group.Count(),
                    OpenJobs = group.Count(job => job.Status == "OPEN"),
                    PendingJobs = group.Count(job => job.Status == "PENDING_REVIEW")
                })
                .FirstOrDefaultAsync();

            var recentJobs = await jobs
                .OrderByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.Id)
                .Select(job => new EmployerDashboardJobDto
                {
                    Id = job.Id,
                    Title = job.Title,
                    Position = job.Position,
                    Status = job.Status,
                    CreatedAt = job.CreatedAt,
                    ApplicationDeadline = job.ApplicationDeadline,
                    ApplicationCount = job.ApplicationCount
                })
                .ToListAsync();

            return new EmployerDashboardDto
            {
                CompanyName = company.CompanyName,
                CompanyStatus = company.Status,
                JobPostCreditsRemaining = company.JobPostCreditsRemaining + availablePaidPosts,
                HasUnlimitedJobPosts = hasUnlimitedJobPosts,
                TotalJobs = summary?.TotalJobs ?? 0,
                OpenJobs = summary?.OpenJobs ?? 0,
                PendingJobs = summary?.PendingJobs ?? 0,
                RecentJobs = recentJobs
            };
        }

        private async Task<bool> ReserveActiveSubscriptionQuotaAsync(long companyId, DateTimeOffset now)
        {
            var availableSubscriptions = await _context.CompanySubscriptions
                .Where(subscription => subscription.CompanyId == companyId
                    && subscription.Status == "ACTIVE"
                    && subscription.StartAt <= now
                    && subscription.EndAt > now
                    && (subscription.RemainingJobPosts == null || subscription.RemainingJobPosts > 0))
                .OrderBy(subscription => subscription.EndAt)
                .Select(subscription => new { subscription.Id, subscription.RemainingJobPosts })
                .ToListAsync();

            foreach (var subscription in availableSubscriptions)
            {
                if (subscription.RemainingJobPosts is null) return true;

                var reserved = await _context.CompanySubscriptions
                    .Where(item => item.Id == subscription.Id
                        && item.Status == "ACTIVE"
                        && item.StartAt <= now
                        && item.EndAt > now
                        && item.RemainingJobPosts > 0)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.RemainingJobPosts, item => item.RemainingJobPosts - 1)
                        .SetProperty(item => item.UpdatedAt, now));
                if (reserved > 0) return true;
            }

            return false;
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
            return _context.Jobs
                .AsNoTracking()
                .Where(job => job.Status == "OPEN")
                .Where(job => job.Company.Status == "ACTIVE");
        }
    }
}
