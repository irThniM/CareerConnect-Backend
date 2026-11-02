namespace CareerConnect.Domain.Entities;

/// <summary>Planned normalized project/portfolio section for a candidate profile.</summary>
public sealed class CandidateProject
{
    public long Id { get; set; }
    public long CandidateProfileId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? ProjectUrl { get; set; }
    /// <summary>JSON payload until a typed JSONB mapping is implemented.</summary>
    public string TechnologiesJson { get; set; } = "[]";
    public string? Description { get; set; }
    public CandidateProfile CandidateProfile { get; set; } = null!;
}
