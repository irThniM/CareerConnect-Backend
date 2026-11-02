using CareerConnect.Application.Candidates.Models;

namespace CareerConnect.Application.Candidates.Requests
{
    public sealed class UpdateCandidateResumeSectionsRequestDto
    {
        public List<CandidateResumeSectionDto> WorkExperiences { get; set; } = new();
        public List<CandidateResumeSectionDto> Projects { get; set; } = new();
        public List<CandidateResumeSectionDto> Education { get; set; } = new();
    }
}
