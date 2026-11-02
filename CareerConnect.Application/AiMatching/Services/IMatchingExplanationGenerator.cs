using CareerConnect.Application.AiMatching.Models;

namespace CareerConnect.Application.AiMatching.Services;

/// <summary>Optional later Gemini explanation contract. Scoring and rank must remain server-controlled.</summary>
public interface IMatchingExplanationGenerator
{
    Task<string> GenerateExplanationAsync(IReadOnlyList<RagEvidenceDto> evidence, CancellationToken cancellationToken = default);
}
