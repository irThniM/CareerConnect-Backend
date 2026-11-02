using CareerConnect.Application.Applications.Exceptions;
using CareerConnect.Application.Applications.Requests;
using CareerConnect.Application.Applications.Responses;
using CareerConnect.Application.Applications.Services;
using CareerConnect.Application.Candidates.Services;
using CareerConnect.Application.Interviews.Responses;
using CareerConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence;

public sealed class ApplicationService : IApplicationService
{
    private const int MaximumCoverLetterLength = 2000;
    private static readonly HashSet<string> EmployerStatuses = new(StringComparer.Ordinal)
    {
        "REVIEWING", "INTERVIEW", "OFFERED", "REJECTED", "HIRED"
    };

    private readonly AppDbContext _context;
    private readonly IResumeStorageService _resumeStorage;

    public ApplicationService(AppDbContext context, IResumeStorageService resumeStorage)
    {
        _context = context;
        _resumeStorage = resumeStorage;
    }

    public async Task<long> SubmitAsync(
        Guid candidateUserId,
        long jobId,
        SubmitApplicationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.CandidateProfiles
            .Include(candidate => candidate.Resume)
            .FirstOrDefaultAsync(candidate => candidate.UserId == candidateUserId, cancellationToken);

        if (profile is null)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.CandidateProfileNotFound,
                "Không tìm thấy hồ sơ ứng viên.");
        }

        if (profile.Resume is null)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.ResumeRequired,
                "Bạn cần tải CV lên hồ sơ trước khi ứng tuyển.");
        }

        var job = await _context.Jobs
            .Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);

        if (job is null
            || job.Status != "OPEN"
            || job.Company.Status != "ACTIVE"
            || (job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value.Date < DateTime.UtcNow.Date))
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.JobUnavailable,
                "Tin tuyển dụng đã đóng hoặc không còn nhận hồ sơ.");
        }

        var coverLetter = string.IsNullOrWhiteSpace(request.CoverLetter) ? null : request.CoverLetter.Trim();
        if (coverLetter?.Length > MaximumCoverLetterLength)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.CoverLetterTooLong,
                $"Thư ứng tuyển tối đa {MaximumCoverLetterLength} ký tự.");
        }

        var alreadyApplied = await _context.JobApplications.AnyAsync(
            application => application.JobId == jobId && application.CandidateProfileId == profile.Id,
            cancellationToken);
        if (alreadyApplied)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.AlreadyApplied,
                "Bạn đã ứng tuyển vị trí này rồi.");
        }

        var now = DateTimeOffset.UtcNow;
        var application = new JobApplication
        {
            JobId = job.Id,
            CandidateProfileId = profile.Id,
            ResumeId = profile.Resume.Id,
            ResumeCloudinaryPublicIdSnapshot = profile.Resume.CloudinaryPublicId,
            ResumeFileNameSnapshot = profile.Resume.OriginalFileName,
            ResumeContentTypeSnapshot = profile.Resume.ContentType,
            SourceType = "CANDIDATE_APPLY",
            CoverLetter = coverLetter,
            Status = "RECEIVED",
            AppliedAt = now,
            UpdatedAt = now
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            ChangedByUserId = candidateUserId,
            OldStatus = null,
            NewStatus = application.Status,
            Note = "Ứng viên đã gửi hồ sơ.",
            CreatedAt = now
        });

        job.ApplicationCount++;
        job.UpdatedAt = DateTime.UtcNow;
        _context.JobApplications.Add(application);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConstraintErrors.IsApplicationUniqueViolation(exception))
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.AlreadyApplied,
                "Bạn đã ứng tuyển vị trí này rồi.");
        }

        return application.Id;
    }

    public async Task<IReadOnlyList<ApplicationSummaryDto>> GetCandidateApplicationsAsync(
        Guid candidateUserId,
        long? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var profileId = await _context.CandidateProfiles
            .Where(candidate => candidate.UserId == candidateUserId)
            .Select(candidate => (long?)candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.CandidateProfileNotFound,
                "Không tìm thấy hồ sơ ứng viên.");
        }

        var query = BuildApplicationsQuery().Where(application => application.CandidateProfileId == profileId);
        if (jobId.HasValue) query = query.Where(application => application.JobId == jobId.Value);

        var applications = await query
            .OrderByDescending(application => application.AppliedAt)
            .ToListAsync(cancellationToken);
        return applications.Select(application => ToSummary(application)).ToList();
    }

    public async Task<IReadOnlyList<ApplicationSummaryDto>> GetEmployerApplicationsAsync(
        Guid employerUserId,
        long? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var companyIds = await _context.CompanyMembers
            .Where(member => member.UserId == employerUserId && member.Status == "ACTIVE")
            .Select(member => member.CompanyId)
            .ToListAsync(cancellationToken);

        if (companyIds.Count == 0)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.EmployerAccessDenied,
                "Tài khoản chưa được liên kết với doanh nghiệp đang hoạt động.");
        }

        if (jobId.HasValue)
        {
            var ownsJob = await _context.Jobs.AnyAsync(
                job => job.Id == jobId.Value && companyIds.Contains(job.CompanyId),
                cancellationToken);
            if (!ownsJob)
            {
                throw new ApplicationWorkflowException(ApplicationWorkflowError.EmployerAccessDenied,
                    "Tin tuyển dụng không thuộc doanh nghiệp của bạn.");
            }
        }

        var query = BuildApplicationsQuery().Where(application => companyIds.Contains(application.Job.CompanyId));
        if (jobId.HasValue) query = query.Where(application => application.JobId == jobId.Value);

        if (!jobId.HasValue)
        {
            var applications = await query
                .OrderByDescending(application => application.AppliedAt)
                .ToListAsync(cancellationToken);
            return applications.Select(application => ToSummary(application)).ToList();
        }

        var jobEmbedding = await _context.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.JobId == jobId.Value)
            .Select(profile => profile.Embedding)
            .FirstOrDefaultAsync(cancellationToken);

        if (jobEmbedding is null)
        {
            var applications = await query
                .OrderByDescending(application => application.AppliedAt)
                .ToListAsync(cancellationToken);
            return applications.Select(application => ToSummary(application, null)).ToList();
        }

        var rankedApplications = await query
            .Select(application => new
            {
                Application = application,
                Distance = application.CandidateProfile.Embedding == null
                    ? (double?)null
                    : application.CandidateProfile.Embedding.CosineDistance(jobEmbedding)
            })
            .ToListAsync(cancellationToken);

        return rankedApplications
            .OrderBy(result => result.Distance is null)
            .ThenBy(result => result.Distance)
            .ThenByDescending(result => result.Application.AppliedAt)
            .Select(result => ToSummary(result.Application, CalculateMatchPercentage(result.Distance)))
            .ToList();
    }

    public async Task<ApplicationSummaryDto> ChangeStatusAsync(
        Guid employerUserId,
        long applicationId,
        ChangeApplicationStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var application = await _context.JobApplications
            .Include(item => item.Job)
            .ThenInclude(job => job.Company)
            .Include(item => item.CandidateProfile)
            .Include(item => item.StatusHistory)
            .Include(item => item.Interviews)
            .FirstOrDefaultAsync(item => item.Id == applicationId, cancellationToken);

        if (application is null)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.ApplicationNotFound,
                "Không tìm thấy hồ sơ ứng tuyển.");
        }

        await EnsureEmployerOwnsJobAsync(employerUserId, application.Job.CompanyId, cancellationToken);

        var newStatus = request.NewStatus.Trim().ToUpperInvariant();
        if (!EmployerStatuses.Contains(newStatus))
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.InvalidStatus,
                "Trạng thái cần chọn là đang xem xét, phỏng vấn, đề nghị nhận việc, từ chối hoặc đã tuyển.");
        }

        if (application.Status is "HIRED" or "REJECTED")
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.InvalidStatus,
                "Hồ sơ đã kết thúc xử lý nên không thể đổi trạng thái.");
        }

        if (newStatus == "INTERVIEW" && (!request.ScheduledStart.HasValue || request.ScheduledStart.Value <= DateTimeOffset.UtcNow))
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.InvalidStatus,
                "Khi mời phỏng vấn, hãy chọn ngày giờ trong tương lai.");
        }

        var statusChanged = application.Status != newStatus;
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 1000) note = note[..1000];
        if (statusChanged)
        {
            var now = DateTimeOffset.UtcNow;
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                ChangedByUserId = employerUserId,
                OldStatus = application.Status,
                NewStatus = newStatus,
                Note = note,
                CreatedAt = now
            });
            application.Status = newStatus;
            application.UpdatedAt = now;
        }

        if (newStatus == "INTERVIEW" && request.ScheduledStart.HasValue)
        {
            foreach (var previous in application.Interviews.Where(interview => interview.Status == "PROPOSED"))
            {
                previous.Status = "CANCELLED";
                previous.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var now = DateTimeOffset.UtcNow;
            application.Interviews.Add(new InterviewSchedule
            {
                ApplicationId = application.Id,
                ProposedByUserId = employerUserId,
                ScheduledStart = request.ScheduledStart.Value,
                Note = note,
                Status = "PROPOSED",
                CreatedAt = now,
                UpdatedAt = now
            });
            application.UpdatedAt = now;
        }
        else if (statusChanged && newStatus != "INTERVIEW")
        {
            foreach (var pendingInterview in application.Interviews.Where(interview => interview.Status == "PROPOSED"))
            {
                pendingInterview.Status = "CANCELLED";
                pendingInterview.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (statusChanged || (newStatus == "INTERVIEW" && request.ScheduledStart.HasValue))
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ToSummary(application);
    }

    public async Task<ApplicationResumeFileDto> GetEmployerResumeAsync(
        Guid employerUserId,
        long applicationId,
        CancellationToken cancellationToken = default)
    {
        var application = await _context.JobApplications
            .AsNoTracking()
            .Include(item => item.Job)
            .ThenInclude(job => job.Company)
            .FirstOrDefaultAsync(item => item.Id == applicationId, cancellationToken);

        if (application is null)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.ApplicationNotFound,
                "Không tìm thấy hồ sơ ứng tuyển.");
        }

        await EnsureEmployerOwnsJobAsync(employerUserId, application.Job.CompanyId, cancellationToken);

        var extension = Path.GetExtension(application.ResumeFileNameSnapshot).TrimStart('.').ToLowerInvariant();
        var content = await _resumeStorage.DownloadAsync(
            application.ResumeCloudinaryPublicIdSnapshot,
            application.ResumeFileNameSnapshot,
            extension,
            cancellationToken);
        return new ApplicationResumeFileDto(
            application.ResumeFileNameSnapshot,
            application.ResumeContentTypeSnapshot,
            content);
    }

    private IQueryable<JobApplication> BuildApplicationsQuery() => _context.JobApplications
        .AsNoTracking()
        .Include(application => application.Job)
            .ThenInclude(job => job.Company)
        .Include(application => application.CandidateProfile)
        .Include(application => application.StatusHistory)
        .Include(application => application.Interviews);

    private async Task EnsureEmployerOwnsJobAsync(
        Guid employerUserId,
        long companyId,
        CancellationToken cancellationToken)
    {
        var isMember = await _context.CompanyMembers.AnyAsync(
            member => member.UserId == employerUserId
                && member.CompanyId == companyId
                && member.Status == "ACTIVE",
            cancellationToken);
        if (!isMember)
        {
            throw new ApplicationWorkflowException(ApplicationWorkflowError.EmployerAccessDenied,
                "Bạn không có quyền xem hoặc cập nhật hồ sơ của tin tuyển dụng này.");
        }
    }

    private static ApplicationSummaryDto ToSummary(JobApplication application, int? matchPercentage = null) => new()
    {
        Id = application.Id,
        JobId = application.JobId,
        JobTitle = application.Job.Title,
        CompanyName = application.Job.Company.CompanyName,
        CandidateName = application.CandidateProfile.FullName,
        CandidateTitle = application.CandidateProfile.ProfessionalTitle,
        MatchPercentage = matchPercentage,
        ResumeFileName = application.ResumeFileNameSnapshot,
        CoverLetter = application.CoverLetter,
        Status = application.Status,
        AppliedAt = application.AppliedAt,
        UpdatedAt = application.UpdatedAt,
        Interviews = application.Interviews
            .OrderByDescending(interview => interview.ScheduledStart)
            .Select(interview => new InterviewScheduleDto
            {
                Id = interview.Id,
                ApplicationId = interview.ApplicationId,
                ScheduledStart = interview.ScheduledStart,
                Status = interview.Status,
                Note = interview.Note,
                ResponseNote = interview.ResponseNote
            })
            .ToList(),
        StatusHistory = application.StatusHistory
            .OrderBy(history => history.CreatedAt)
            .Select(history => new ApplicationStatusHistoryDto
            {
                OldStatus = history.OldStatus,
                NewStatus = history.NewStatus,
                Note = history.Note,
                CreatedAt = history.CreatedAt
            })
            .ToList()
    };

    private static int? CalculateMatchPercentage(double? cosineDistance) => cosineDistance.HasValue
        ? (int)Math.Round(Math.Clamp((1d - cosineDistance.Value) * 100d, 0d, 100d))
        : null;
}
