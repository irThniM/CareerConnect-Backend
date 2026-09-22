using CareerConnect.Domain.Enums;

namespace CareerConnect.Domain.Entities
{
    public class User
    {
        // Định danh duy nhất (UUID)[cite: 1]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Email đăng nhập và nhận thông báo, lưu chữ thường[cite: 1]
        public string Email { get; set; } = string.Empty;

        // Mật khẩu đã băm; NULL nếu chỉ dùng Google[cite: 1]
        public string? PasswordHash { get; set; }

        // Loại tài khoản cấp hệ thống[cite: 1]
        public AccountType AccountType { get; set; }

        // Trạng thái tài khoản (Mặc định là UNVERIFIED)[cite: 1]
        public UserStatus Status { get; set; } = UserStatus.Unverified;

        // Dành cho đăng nhập Google[cite: 1]
        public string? GoogleId { get; set; }

        // Thời điểm tạo[cite: 1]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Thời điểm cập nhật[cite: 1]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;


        // ==========================================
        // NAVIGATION PROPERTIES (Đã có Entity)
        // ==========================================

        public virtual CandidateProfile? CandidateProfile { get; set; }
        public virtual AdminProfile? AdminProfile { get; set; } // Mới thêm cho Admin

        public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
        public virtual ICollection<CompanyMember> CompanyMembers { get; set; } = new List<CompanyMember>();

        // ==========================================
        // CHƯA CÓ ENTITY (Tạm thời comment lại)
        // ==========================================

        // public virtual ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
        // public virtual ICollection<UserSecurityToken> SecurityTokens { get; set; } = new List<UserSecurityToken>();
        // public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        // public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
        // public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
