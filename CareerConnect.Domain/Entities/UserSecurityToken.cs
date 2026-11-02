namespace CareerConnect.Domain.Entities;

/// <summary>Planned persisted email-verification and password-reset token.</summary>
public sealed class UserSecurityToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = null!;
}
