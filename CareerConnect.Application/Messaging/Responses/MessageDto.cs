namespace CareerConnect.Application.Messaging.Responses;

public sealed class MessageDto
{
    public long Id { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
