namespace CareerConnect.Application.Interviews.Requests;

public sealed class RespondInterviewRequestDto
{
    public string Response { get; set; } = string.Empty;
    public string? ResponseNote { get; set; }
    public DateTimeOffset? RequestedNewTime { get; set; }
}
