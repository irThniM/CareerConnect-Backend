namespace CareerConnect.Domain.Entities;

/// <summary>Planned persisted hiring relationship from candidate apply or accepted invite.</summary>
public sealed class JobApplication
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public long CandidateProfileId { get; set; }
    public long? ResumeId { get; set; }
    public string ResumeCloudinaryPublicIdSnapshot { get; set; } = string.Empty;
    public string ResumeFileNameSnapshot { get; set; } = string.Empty;
    public string ResumeContentTypeSnapshot { get; set; } = string.Empty;
    public long? InvitationId { get; set; }
    public string SourceType { get; set; } = "CANDIDATE_APPLY";
    public string? CoverLetter { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Job Job { get; set; } = null!;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public Resume? Resume { get; set; }
    public JobInvitation? Invitation { get; set; }
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public Conversation? Conversation { get; set; }
    public ICollection<InterviewSchedule> Interviews { get; set; } = new List<InterviewSchedule>();
}
