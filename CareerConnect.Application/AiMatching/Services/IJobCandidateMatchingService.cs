using CareerConnect.Application.AiMatching.Responses;

namespace CareerConnect.Application.AiMatching.Services;

/// <summary>Planned reverse recommendation flow; candidate-side job recommendations already exist.</summary>
public interface IJobCandidateMatchingService
{
    Task<IReadOnlyList<CandidateRecommendationDto>> GetCandidatesForJobAsync(Guid employerUserId, long jobId, CancellationToken cancellationToken = default);
    Task RefreshMatchesForCandidateAsync(long candidateProfileId, CancellationToken cancellationToken = default);
    Task RefreshMatchesForJobAsync(long jobId, CancellationToken cancellationToken = default);
}
