namespace CareerConnect.Domain.Entities
{
    public class AdminProfile
    {
        public long Id { get; set; }

        // Liên kết 1-1 với bảng Users
        public Guid UserId { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? PhoneNumber { get; set; }

        // Phân quyền chi tiết nội bộ Admin (SUPER_ADMIN, MODERATOR, REVIEWER...)
        public string RoleCode { get; set; } = "SUPER_ADMIN";

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}