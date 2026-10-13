namespace CareerConnect.Application.Jobs.Responses
{
    public class JobListItemDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? CategoryName { get; set; }
        public string? Location { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public bool IsSalaryNegotiable { get; set; }
        public string EmploymentType { get; set; } = string.Empty;
        public string WorkMode { get; set; } = string.Empty;
        public string? ExperienceRequired { get; set; }
        public int Quantity { get; set; }
        public DateTime? ApplicationDeadline { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> RequiredSkills { get; set; } = new();
    }
}
