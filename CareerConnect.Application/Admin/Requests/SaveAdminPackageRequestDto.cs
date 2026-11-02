namespace CareerConnect.Application.Admin.Requests;

public sealed class SaveAdminPackageRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? MemberLimit { get; set; }
    public int? JobPostLimit { get; set; }
    public bool IsActive { get; set; }
}
