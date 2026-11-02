namespace CareerConnect.Application.Messaging.Responses;

public sealed class ConversationSummaryDto
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string OtherParticipantName { get; set; } = string.Empty;
    public string? LastMessagePreview { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
}
