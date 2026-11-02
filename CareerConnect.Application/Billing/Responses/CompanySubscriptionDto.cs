namespace CareerConnect.Application.Billing.Responses;

public sealed class CompanySubscriptionDto
{
    public string PackageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public int? RemainingJobPosts { get; set; }
    public int? MemberLimit { get; set; }
}
