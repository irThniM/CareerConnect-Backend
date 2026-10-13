namespace CareerConnect.Domain.Entities
{
    public class Job
    {
        public long Id { get; set; }
        public long CompanyId { get; set; }
        public long CreatedByMemberId { get; set; }
        public long? CategoryId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;

        // DRAFT, PENDING_REVIEW, OPEN, CLOSED
        public string Status { get; set; } = "PENDING_REVIEW";

        public DateTime? ApplicationDeadline { get; set; }
        public int ApplicationCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public CompanyProfile Company { get; set; } = null!;
        public CompanyMember CreatedByMember { get; set; } = null!;
        public JobCategory? Category { get; set; }
        public JobProfile JobProfile { get; set; } = null!;
    }
}
