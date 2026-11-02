namespace CareerConnect.Application.Candidates.Services
{
    public sealed record ResumeSkill(string Name, string Category);

    public sealed record ResumeSectionItem(string Title, string? Organization, string? Period, string? Description);

    public sealed record ResumeProcessingResult(
        string ExtractedText,
        string ExtractionMethod,
        IReadOnlyList<ResumeSkill> Skills,
        string? SuggestedSummary,
        string? SuggestedFullName,
        string? SuggestedPhoneNumber,
        string? SuggestedLocation,
        IReadOnlyList<ResumeSectionItem> WorkExperiences,
        IReadOnlyList<ResumeSectionItem> Projects,
        IReadOnlyList<ResumeSectionItem> Education);

    public interface IResumeProcessingService
    {
        Task<ResumeProcessingResult> ProcessAsync(
            Stream file,
            string extension,
            CancellationToken cancellationToken = default);
    }
}
