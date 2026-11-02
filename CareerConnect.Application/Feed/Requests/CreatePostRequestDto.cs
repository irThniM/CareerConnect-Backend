namespace CareerConnect.Application.Feed.Requests;

public sealed class CreatePostRequestDto
{
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = "OTHER";
}
