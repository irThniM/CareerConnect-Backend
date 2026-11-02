namespace CareerConnect.Domain.Entities;

/// <summary>Planned employer-initiated invitation to apply for a job.</summary>
public sealed class JobInvitation
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public long CandidateProfileId { get; set; }
    public long InvitedByMemberId { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
    public Job Job { get; set; } = null!;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public CompanyMember InvitedByMember { get; set; } = null!;
}
