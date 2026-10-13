using Pgvector;

namespace CareerConnect.Domain.Entities
{
    public class JobProfile
    {
        public long Id { get; set; }
        public long JobId { get; set; } // Liên kết 1-1 với Job

        public string EmploymentType { get; set; } = string.Empty; // FULL_TIME, PART_TIME...
        public string WorkMode { get; set; } = string.Empty; // ONSITE, REMOTE...
        public int Quantity { get; set; } = 1;
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public bool IsSalaryNegotiable { get; set; }

        public string? ExperienceRequired { get; set; }
        public string? EducationRequired { get; set; }
        public string? Location { get; set; }

        public string JobDescription { get; set; } = string.Empty;
        public string JobRequirements { get; set; } = string.Empty;
        public string? JobBenefits { get; set; }
        public string? Note { get; set; }

        public string? NormalizedText { get; set; }

        // Dimension is configured with the pgvector column in AppDbContext.
        public Vector? Embedding { get; set; }
        public DateTime? EmbeddingUpdatedAt { get; set; }

        public Job Job { get; set; } = null!;
        public ICollection<JobRequiredSkill> RequiredSkills { get; set; } = new List<JobRequiredSkill>();
    }
}