namespace CareerConnect.Domain.Entities;

/// <summary>Optional planned subscription and company quota period.</summary>
public sealed class CompanySubscription
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long PackageId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public int? RemainingJobPosts { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CompanyProfile Company { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
