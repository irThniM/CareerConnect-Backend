namespace CareerConnect.Domain.Entities;

/// <summary>Planned normalized education section for a candidate profile.</summary>
public sealed class CandidateEducation
{
    public long Id { get; set; }
    public long CandidateProfileId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? Degree { get; set; }
    public decimal? Gpa { get; set; }
    public string? Description { get; set; }
    public CandidateProfile CandidateProfile { get; set; } = null!;
}
