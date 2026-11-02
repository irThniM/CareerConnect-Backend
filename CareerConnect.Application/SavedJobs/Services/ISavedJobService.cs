using CareerConnect.Application.SavedJobs.Responses;

namespace CareerConnect.Application.SavedJobs.Services;

public interface ISavedJobService
{
    Task<IReadOnlyList<SavedJobDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid userId, long jobId, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid userId, long jobId, CancellationToken cancellationToken = default);
}
