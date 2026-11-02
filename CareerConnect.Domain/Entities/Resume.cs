namespace CareerConnect.Domain.Entities
{
    /// <summary>
    /// CV hiện hành của một ứng viên. Nội dung file được lưu trên Cloudinary;
    /// bảng này chỉ lưu metadata và Cloudinary public ID.
    /// </summary>
    public class Resume
    {
        public long Id { get; set; }
        public long CandidateProfileId { get; set; }
        public string SourceType { get; set; } = "UPLOADED";
        public string FileUrl { get; set; } = string.Empty;
        public string CloudinaryPublicId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? ExtractedText { get; set; }
        public string? StructuredData { get; set; }
        public string? ExtractionMethod { get; set; }
        public string ProcessingStatus { get; set; } = "PENDING";
        public string? ProcessingError { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public CandidateProfile CandidateProfile { get; set; } = null!;
    }
}
