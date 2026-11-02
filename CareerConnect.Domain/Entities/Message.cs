namespace CareerConnect.Domain.Entities;

/// <summary>Planned persisted text message with an optional simple attachment URL.</summary>
public sealed class Message
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
