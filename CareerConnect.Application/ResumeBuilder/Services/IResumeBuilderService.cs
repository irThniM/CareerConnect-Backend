using CareerConnect.Application.ResumeBuilder.Requests;

namespace CareerConnect.Application.ResumeBuilder.Services;

public interface IResumeBuilderService
{
    Task<IReadOnlyList<object>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveVersionAsync(Guid candidateUserId, SaveResumeDraftRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<object>> GetVersionHistoryAsync(Guid candidateUserId, CancellationToken cancellationToken = default);
    Task RestoreVersionAsync(Guid candidateUserId, int versionNo, CancellationToken cancellationToken = default);
}
