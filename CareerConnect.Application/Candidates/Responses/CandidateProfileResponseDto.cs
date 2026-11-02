namespace CareerConnect.Application.Candidates.Responses
{
    public class CandidateProfileResponseDto
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfessionalTitle { get; set; }
        public string? Address { get; set; }
        public string? Summary { get; set; }
        public bool IsLookingForJob { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Skills { get; set; } = new();
        public CandidateResumeResponseDto? Resume { get; set; }
    }
}
