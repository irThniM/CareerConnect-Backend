namespace CareerConnect.Application.ProfessionalDevelopment.Models;

/// <summary>Optional outline-only course contract; this module is not in entity design v1.2.</summary>
public sealed class CourseSummaryDraft
{
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Url { get; set; }
}
