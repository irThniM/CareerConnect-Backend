namespace CareerConnect.Application.Admin.Responses;

public sealed class AdminPackageDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? MemberLimit { get; set; }
    public int? JobPostLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AdminPackageDeleteResultDto
{
    public bool Deleted { get; set; }
    public bool IsActive { get; set; }
    public string Message { get; set; } = string.Empty;
}
