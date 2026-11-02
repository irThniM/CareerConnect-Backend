namespace CareerConnect.Domain.Entities;

/// <summary>Planned application-scoped conversation; messages remain persisted in PostgreSQL.</summary>
public sealed class Conversation
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public JobApplication Application { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
