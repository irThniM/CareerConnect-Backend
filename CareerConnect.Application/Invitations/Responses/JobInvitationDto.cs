namespace CareerConnect.Application.Invitations.Responses;

public sealed class JobInvitationDto
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public long CandidateProfileId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
