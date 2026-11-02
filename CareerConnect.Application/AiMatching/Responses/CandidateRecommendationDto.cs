namespace CareerConnect.Application.AiMatching.Responses;

public sealed class CandidateRecommendationDto
{
    public long CandidateProfileId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public decimal TotalScore { get; set; }
    public IReadOnlyList<string> MatchedSkills { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingSkills { get; set; } = Array.Empty<string>();
}
