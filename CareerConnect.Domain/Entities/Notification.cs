namespace CareerConnect.Domain.Entities;

/// <summary>Planned in-app notification record.</summary>
public sealed class Notification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "SYSTEM";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = null!;
}
