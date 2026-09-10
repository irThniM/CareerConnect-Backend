namespace CareerConnect.Domain.Entities
{
    public class UserSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string RefreshTokenHash { get; set; } = string.Empty; // Refresh token đã băm
        public DateTime ExpiresAt { get; set; }                      // Hạn sử dụng (TIMESTAMPTZ)
        public DateTime? RevokedAt { get; set; }                     // Thời điểm thu hồi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
