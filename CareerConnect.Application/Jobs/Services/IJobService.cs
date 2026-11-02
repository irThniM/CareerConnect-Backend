using CareerConnect.Application.Employer.Requests;
using CareerConnect.Application.Jobs.Requests;
using CareerConnect.Application.Jobs.Responses;
using CareerConnect.Application.Employer.Responses;

namespace CareerConnect.Application.Jobs.Services;

public interface IJobService
{
    Task<string> CreateJobAsync(Guid userId, CreateJobRequestDto request);
    Task<CreateJobRequestDto?> GetEmployerJobForEditAsync(Guid userId, long jobId, CancellationToken cancellationToken = default);
    Task<bool> UpdateJobAsync(Guid userId, long jobId, CreateJobRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> ExtendJobDeadlineAsync(Guid userId, long jobId, DateTime applicationDeadline, CancellationToken cancellationToken = default);
    Task<EmployerDashboardDto?> GetEmployerDashboardAsync(Guid userId);
    Task<IReadOnlyList<JobCategoryDto>> GetActiveJobCategoriesAsync();
    Task<JobSearchResponseDto> SearchPublicJobsAsync(JobSearchRequestDto request);
    Task<JobDetailsDto?> GetPublicJobDetailsAsync(long jobId);
}
