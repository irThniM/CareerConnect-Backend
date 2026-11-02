using CareerConnect.Application.Invitations.Requests;
using CareerConnect.Application.Invitations.Responses;

namespace CareerConnect.Application.Invitations.Services;

public interface IJobInvitationService
{
    Task<long> InviteAsync(Guid employerUserId, CreateJobInvitationRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobInvitationDto>> GetCandidateInboxAsync(Guid candidateUserId, CancellationToken cancellationToken = default);
    Task RespondAsync(Guid candidateUserId, long invitationId, RespondJobInvitationRequestDto request, CancellationToken cancellationToken = default);
}
