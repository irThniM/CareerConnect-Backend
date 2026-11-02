namespace CareerConnect.Application.AiMatching.Responses;

public sealed class MatchExplanationDto
{
    public decimal TotalScore { get; set; }
    public decimal SemanticScore { get; set; }
    public decimal SkillScore { get; set; }
    public decimal ExperienceScore { get; set; }
    public decimal EducationScore { get; set; }
    public decimal LocationScore { get; set; }
    public decimal WorkModeScore { get; set; }
    public IReadOnlyList<string> MatchedSkills { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingSkills { get; set; } = Array.Empty<string>();
    public string? Explanation { get; set; }
}
