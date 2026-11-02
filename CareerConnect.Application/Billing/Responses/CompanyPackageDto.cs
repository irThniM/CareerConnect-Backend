namespace CareerConnect.Application.Billing.Responses;

public sealed class CompanyPackageDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? MemberLimit { get; set; }
    public int? JobPostLimit { get; set; }
}
