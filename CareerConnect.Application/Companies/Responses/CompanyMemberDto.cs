namespace CareerConnect.Application.Companies.Responses;

public sealed class CompanyMemberDto
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
