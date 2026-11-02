namespace CareerConnect.Application.Applications.Requests;

public sealed class ChangeApplicationStatusRequestDto
{
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset? ScheduledStart { get; set; }
}
