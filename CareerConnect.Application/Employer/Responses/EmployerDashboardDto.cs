namespace CareerConnect.Application.Employer.Responses;

public sealed class EmployerDashboardDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyStatus { get; set; } = string.Empty;
    public int JobPostCreditsRemaining { get; set; }
    public bool HasUnlimitedJobPosts { get; set; }
    public int TotalJobs { get; set; }
    public int OpenJobs { get; set; }
    public int PendingJobs { get; set; }
    public List<EmployerDashboardJobDto> RecentJobs { get; set; } = new();
}

public sealed class EmployerDashboardJobDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public int ApplicationCount { get; set; }
}
