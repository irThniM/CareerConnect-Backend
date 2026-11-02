namespace CareerConnect.Domain.Entities;

/// <summary>Planned priority-two saved-job relation.</summary>
public sealed class SavedJob
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public long JobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
