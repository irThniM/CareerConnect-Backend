namespace CareerConnect.Domain.Entities
{
    public class CompanyMember
    {
        public long Id { get; set; }
        public long CompanyId { get; set; }
        public Guid UserId { get; set; }
        public string MemberRole { get; set; } = "OWNER"; // OWNER, RECRUITER
        public string Status { get; set; } = "ACTIVE";     // ACTIVE, SUSPENDED
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? ContactEmail { get; set; }
        public string? ZaloNumber { get; set; }
        public string? FacebookUrl { get; set; }
        public string ContactVisibility { get; set; } = "{\"show_email\": true, \"show_zalo\": true, \"show_fb\": false}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Company Company { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
