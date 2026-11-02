using CareerConnect.Application.Jobs.Responses;

namespace CareerConnect.Application.Candidates.Responses;

public sealed class CandidateJobRecommendationsResponseDto
{
    public bool IsProfileReady { get; set; }
    public string? Message { get; set; }
    public int TotalCount { get; set; }
    public List<CandidateJobRecommendationDto> Items { get; set; } = [];
}

public sealed class CandidateJobRecommendationDto
{
    public JobListItemDto Job { get; set; } = new();
    public int SimilarityPercentage { get; set; }
    public bool SameArea { get; set; }
    public bool Remote { get; set; }
}
