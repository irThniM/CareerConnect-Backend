namespace CareerConnect.Domain.Entities
{
    /// <summary>
    /// Kỹ năng đã chuẩn hóa của ứng viên. SourceType hiện ghi nhận kỹ năng lấy từ CV.
    /// </summary>
    public class CandidateSkill
    {
        public long Id { get; set; }
        public long CandidateProfileId { get; set; }
        public long SkillId { get; set; }
        public string SourceType { get; set; } = "CV";

        public CandidateProfile CandidateProfile { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
