namespace CareerConnect.Domain.Entities
{
    public class CompanyProfile
    {
        public long Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? TaxStatus { get; set; }
        public string? Website { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Address { get; set; }
        public string? ContactEmail { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, BANNED
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<CompanyMember> CompanyMembers { get; set; } = new List<CompanyMember>();
    }
}
