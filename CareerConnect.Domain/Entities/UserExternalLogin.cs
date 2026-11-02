namespace CareerConnect.Domain.Entities;

/// <summary>Planned external identity link. Not registered in AppDbContext yet.</summary>
public sealed class UserExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "GOOGLE";
    public string ProviderUserId { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = null!;
}
