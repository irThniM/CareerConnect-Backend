using CareerConnect.Application.Candidates.Services;

namespace CareerConnect.Application.Candidates.Models
{
    public sealed class CandidateResumeSectionDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Organization { get; set; }
        public string? Period { get; set; }
        public string? Description { get; set; }
    }

    public sealed class CandidateResumeSkillDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// JSON stored in resumes.StructuredData. Kept together with the CV so no
    /// additional database tables are required for the first profile version.
    /// </summary>
    public sealed class CandidateResumeStructuredData
    {
        public List<CandidateResumeSkillDto> ExtractedSkills { get; set; } = new();
        public string? SuggestedFullName { get; set; }
        public string? SuggestedSummary { get; set; }
        public int TextLength { get; set; }
        public List<CandidateResumeSectionDto> WorkExperiences { get; set; } = new();
        public List<CandidateResumeSectionDto> Projects { get; set; } = new();
        public List<CandidateResumeSectionDto> Education { get; set; } = new();
    }
}
