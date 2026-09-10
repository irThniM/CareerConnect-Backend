namespace CareerConnect.Domain.Entities
{
    public class CandidateProfile
    {
        public long Id { get; set; }

        // Liên kết 1-1 với bảng Users[cite: 1]
        public Guid UserId { get; set; }

        public string FullName { get; set; } = string.Empty; // Họ tên hiển thị[cite: 1]
        public string? AvatarUrl { get; set; } // Ảnh đại diện Cloudinary[cite: 1]
        public string? PhoneNumber { get; set; } // Số điện thoại[cite: 1]
        public string? Address { get; set; } // Khu vực sinh sống[cite: 1]
        public string? ProfessionalTitle { get; set; } // Chức danh hiện tại[cite: 1]
        public string? Summary { get; set; } // Giới thiệu ngắn[cite: 1]
        public string? PortfolioUrl { get; set; } // Portfolio/GitHub/website chính[cite: 1]

        public bool IsLookingForJob { get; set; } = true; // Trạng thái sẵn sàng tìm việc[cite: 1]
        public bool RecruiterSearchEnabled { get; set; } = false; // Cho phép nhà tuyển dụng tìm thấy hồ sơ[cite: 1]

        public string? NormalizedText { get; set; } // Nội dung chuẩn hóa phục vụ tìm kiếm/AI[cite: 1]

        // Lưu ý: Trường Vector Embedding sẽ cần cài extension pgvector trong PostgreSQL
        public string? Embedding { get; set; } // Vector hồ sơ[cite: 1] 
        public DateTime? EmbeddingUpdatedAt { get; set; } // Lần cập nhật embedding[cite: 1]

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Thời điểm tạo[cite: 1]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // Thời điểm cập nhật[cite: 1]

        // Navigation property
        public User User { get; set; } = null!;
    }
}
