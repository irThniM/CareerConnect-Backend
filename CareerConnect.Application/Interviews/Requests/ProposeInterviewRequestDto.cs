namespace CareerConnect.Application.Interviews.Requests;

public sealed class ProposeInterviewRequestDto
{
    public DateTimeOffset ScheduledStart { get; set; }
    public string? Note { get; set; }
}
