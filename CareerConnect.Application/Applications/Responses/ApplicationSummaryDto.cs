namespace CareerConnect.Application.Applications.Responses;

using CareerConnect.Application.Interviews.Responses;

public sealed class ApplicationSummaryDto
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string? CandidateTitle { get; set; }
    public int? MatchPercentage { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public string? CoverLetter { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset AppliedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ApplicationStatusHistoryDto> StatusHistory { get; set; } = new();
    public List<InterviewScheduleDto> Interviews { get; set; } = new();
}

public sealed class ApplicationStatusHistoryDto
{
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record ApplicationResumeFileDto(string FileName, string ContentType, byte[] Content);
