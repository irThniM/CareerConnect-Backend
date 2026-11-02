using CareerConnect.Application.Interviews.Requests;
using CareerConnect.Application.Interviews.Responses;

namespace CareerConnect.Application.Interviews.Services;

public interface IInterviewScheduleService
{
    Task<long> ProposeAsync(Guid employerUserId, long applicationId, ProposeInterviewRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InterviewScheduleDto>> GetForApplicationAsync(Guid requestingUserId, long applicationId, CancellationToken cancellationToken = default);
    Task RespondAsync(Guid candidateUserId, long interviewId, RespondInterviewRequestDto request, CancellationToken cancellationToken = default);
}
