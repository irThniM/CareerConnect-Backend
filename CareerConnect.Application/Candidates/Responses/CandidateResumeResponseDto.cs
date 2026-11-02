using CareerConnect.Application.Candidates.Models;

namespace CareerConnect.Application.Candidates.Responses
{
    public class CandidateResumeResponseDto
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = string.Empty;
        public string? ProcessingError { get; set; }
        public DateTime UploadedAt { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string? SuggestedFullName { get; set; }
        public List<CandidateResumeSectionDto> WorkExperiences { get; set; } = new();
        public List<CandidateResumeSectionDto> Projects { get; set; } = new();
        public List<CandidateResumeSectionDto> Education { get; set; } = new();
    }
}
