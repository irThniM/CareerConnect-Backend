namespace CareerConnect.Domain.Entities
{
    public class JobRequiredSkill
    {
        public long Id { get; set; }
        public long JobProfileId { get; set; }
        public long SkillId { get; set; }

        public JobProfile JobProfile { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
