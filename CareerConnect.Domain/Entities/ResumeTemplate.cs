namespace CareerConnect.Domain.Entities;

/// <summary>Optional planned template for CVs built inside the website.</summary>
public sealed class ResumeTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    /// <summary>JSON layout definition until JSONB mapping is implemented.</summary>
    public string TemplateDataJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
