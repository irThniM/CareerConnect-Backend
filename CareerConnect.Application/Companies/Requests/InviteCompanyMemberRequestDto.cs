namespace CareerConnect.Application.Companies.Requests;

public sealed class InviteCompanyMemberRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "RECRUITER";
}
