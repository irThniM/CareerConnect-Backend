namespace CareerConnect.Domain.Entities;

/// <summary>Planned interview proposal, response, rescheduling, cancellation and completion.</summary>
public sealed class InterviewSchedule
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string? ResponseNote { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public JobApplication Application { get; set; } = null!;
    public User ProposedByUser { get; set; } = null!;
}
