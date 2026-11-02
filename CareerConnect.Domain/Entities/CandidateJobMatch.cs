namespace CareerConnect.Domain.Entities;

/// <summary>Planned persisted, explainable match result independent of an application.</summary>
public sealed class CandidateJobMatch
{
    public long Id { get; set; }
    public long CandidateProfileId { get; set; }
    public long JobId { get; set; }
    public decimal TotalScore { get; set; }
    public decimal SemanticScore { get; set; }
    public decimal SkillScore { get; set; }
    public decimal ExperienceScore { get; set; }
    public decimal EducationScore { get; set; }
    public decimal LocationScore { get; set; }
    public decimal WorkModeScore { get; set; }
    /// <summary>JSON payloads until JSONB configuration is added.</summary>
    public string MatchedSkillsJson { get; set; } = "[]";
    public string MissingSkillsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
