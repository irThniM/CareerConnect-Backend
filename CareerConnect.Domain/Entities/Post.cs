namespace CareerConnect.Domain.Entities;

/// <summary>Planned professional-network feed post.</summary>
public sealed class Post
{
    public long Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>JSON array of Cloudinary URLs until JSONB mapping is added.</summary>
    public string ImageUrlsJson { get; set; } = "[]";
    public string Category { get; set; } = "OTHER";
    public string Status { get; set; } = "PUBLISHED";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User Author { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
