namespace CareerConnect.Domain.Entities;

/// <summary>Planned single-level comment on a professional-network post.</summary>
public sealed class Comment
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "PUBLISHED";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Post Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
