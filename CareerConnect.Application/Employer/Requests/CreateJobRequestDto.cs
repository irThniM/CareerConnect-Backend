namespace CareerConnect.Application.Employer.Requests
{
    public class CreateJobRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public long? CategoryId { get; set; }
        public DateTime? ApplicationDeadline { get; set; }

        public string EmploymentType { get; set; } = string.Empty;
        public string WorkMode { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public bool IsSalaryNegotiable { get; set; } = false;
        public string? ExperienceRequired { get; set; }
        public string? EducationRequired { get; set; }
        public string? Location { get; set; }

        public string JobDescription { get; set; } = string.Empty;
        public string JobRequirements { get; set; } = string.Empty;
        public string? JobBenefits { get; set; }

        public List<long> RequiredSkillIds { get; set; } = new();
    }
}