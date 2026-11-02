namespace CareerConnect.Domain.Entities;

/// <summary>Optional planned snapshot history for CVs built online, not uploaded PDFs.</summary>
public sealed class ResumeVersion
{
    public long Id { get; set; }
    public long CandidateProfileId { get; set; }
    public long? TemplateId { get; set; }
    public int VersionNo { get; set; }
    /// <summary>JSON snapshot until JSONB mapping is implemented.</summary>
    public string ResumeDataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public ResumeTemplate? Template { get; set; }
}
