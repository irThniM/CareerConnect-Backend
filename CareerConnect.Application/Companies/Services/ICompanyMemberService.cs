using CareerConnect.Application.Companies.Requests;
using CareerConnect.Application.Companies.Responses;

namespace CareerConnect.Application.Companies.Services;

public interface ICompanyMemberService
{
    Task<IReadOnlyList<CompanyMemberDto>> GetMembersAsync(Guid employerUserId, CancellationToken cancellationToken = default);
    Task InviteAsync(Guid ownerUserId, InviteCompanyMemberRequestDto request, CancellationToken cancellationToken = default);
    Task ChangeRoleAsync(Guid ownerUserId, long memberId, string role, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid ownerUserId, long memberId, CancellationToken cancellationToken = default);
}
