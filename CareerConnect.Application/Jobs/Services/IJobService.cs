using CareerConnect.Application.Employer.Requests;
using CareerConnect.Application.Jobs.Requests;
using CareerConnect.Application.Jobs.Responses;

namespace CareerConnect.Application.Jobs.Services;

public interface IJobService
{
    Task<string> CreateJobAsync(Guid userId, CreateJobRequestDto request);
    Task<IReadOnlyList<JobCategoryDto>> GetActiveJobCategoriesAsync();
    Task<JobSearchResponseDto> SearchPublicJobsAsync(JobSearchRequestDto request);
    Task<JobDetailsDto?> GetPublicJobDetailsAsync(long jobId);
}
