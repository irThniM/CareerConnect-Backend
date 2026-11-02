namespace CareerConnect.Application.Interviews.Responses;

public sealed class InterviewScheduleDto
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? ResponseNote { get; set; }
}
