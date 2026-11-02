namespace CareerConnect.Application.Feed.Responses;

public sealed class FeedPostDto
{
    public long Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<FeedCommentDto> Comments { get; set; } = Array.Empty<FeedCommentDto>();
}

public sealed class FeedCommentDto
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
