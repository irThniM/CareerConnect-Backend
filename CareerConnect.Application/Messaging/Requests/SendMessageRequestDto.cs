namespace CareerConnect.Application.Messaging.Requests;

public sealed class SendMessageRequestDto
{
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}
