using CareerConnect.Application.Applications.Requests;
using CareerConnect.Application.Applications.Responses;

namespace CareerConnect.Application.Applications.Services;

public interface IApplicationService
{
    Task<long> SubmitAsync(Guid candidateUserId, long jobId, SubmitApplicationRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationSummaryDto>> GetCandidateApplicationsAsync(Guid candidateUserId, long? jobId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationSummaryDto>> GetEmployerApplicationsAsync(Guid employerUserId, long? jobId = null, CancellationToken cancellationToken = default);
    Task<ApplicationSummaryDto> ChangeStatusAsync(Guid employerUserId, long applicationId, ChangeApplicationStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<ApplicationResumeFileDto> GetEmployerResumeAsync(Guid employerUserId, long applicationId, CancellationToken cancellationToken = default);
}
