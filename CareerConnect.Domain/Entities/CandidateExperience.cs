namespace CareerConnect.Domain.Entities;

/// <summary>Planned normalized work-history section for a candidate profile.</summary>
public sealed class CandidateExperience
{
    public long Id { get; set; }
    public long CandidateProfileId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
    public CandidateProfile CandidateProfile { get; set; } = null!;
}
