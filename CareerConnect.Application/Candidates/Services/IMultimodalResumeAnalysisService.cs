using CareerConnect.Application.Candidates.Models;

namespace CareerConnect.Application.Candidates.Services;

/// <summary>Optional scanned/image resume analysis contract from the outline; no Gemini call is implemented here.</summary>
public interface IMultimodalResumeAnalysisService
{
    Task<CandidateResumeStructuredData> AnalyzeScannedResumeAsync(Stream document, string contentType, CancellationToken cancellationToken = default);
}
