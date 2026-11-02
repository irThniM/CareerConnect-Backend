namespace CareerConnect.Domain.Entities;

/// <summary>Planned audit trail for hiring-stage transitions.</summary>
public sealed class ApplicationStatusHistory
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public JobApplication Application { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
