using CareerConnect.Application.AiMatching.Models;
using CareerConnect.Application.AiMatching.Responses;

namespace CareerConnect.Application.AiMatching.Services;

/// <summary>Planned deterministic score plus optional RAG evidence/Gemini explanation contracts.</summary>
public interface IMatchingExplanationService
{
    Task<MatchExplanationDto?> GetCandidateJobExplanationAsync(Guid candidateUserId, long jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RagEvidenceDto>> RetrieveEvidenceAsync(Guid candidateUserId, long jobId, CancellationToken cancellationToken = default);
}
