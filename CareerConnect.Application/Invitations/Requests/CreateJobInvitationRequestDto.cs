namespace CareerConnect.Application.Invitations.Requests;

public sealed class CreateJobInvitationRequestDto
{
    public long JobId { get; set; }
    public long CandidateProfileId { get; set; }
    public string? Message { get; set; }
}
