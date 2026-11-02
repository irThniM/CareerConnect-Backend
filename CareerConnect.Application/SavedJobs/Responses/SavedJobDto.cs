namespace CareerConnect.Application.SavedJobs.Responses;

public sealed class SavedJobDto
{
    public long JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; }
}
