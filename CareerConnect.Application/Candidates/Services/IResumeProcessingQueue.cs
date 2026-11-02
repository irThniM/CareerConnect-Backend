namespace CareerConnect.Application.Candidates.Services;

/// <summary>Planned in-memory queue contract; do not add a database queue table.</summary>
public interface IResumeProcessingQueue
{
    ValueTask EnqueueAsync(long resumeId, CancellationToken cancellationToken = default);
}
