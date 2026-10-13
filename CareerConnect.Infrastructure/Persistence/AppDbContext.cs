using CareerConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<CandidateProfile> CandidateProfiles { get; set; }
        public DbSet<CompanyProfile> CompanyProfiles { get; set; }
        public DbSet<CompanyMember> CompanyMembers { get; set; }
        public DbSet<AdminProfile> AdminProfiles { get; set; }

        public DbSet<JobCategory> JobCategories { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobProfile> JobProfiles { get; set; }
        public DbSet<JobRequiredSkill> JobRequiredSkills { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");
            base.OnModelCreating(modelBuilder);

            // Cấu hình bảng Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.AccountType).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // Cấu hình bảng UserSession
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.ToTable("user_sessions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.RefreshTokenHash).IsUnique();
                entity.HasOne(e => e.User).WithMany(u => u.Sessions).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // Cấu hình bảng CandidateProfile
            modelBuilder.Entity<CandidateProfile>(entity =>
            {
                entity.ToTable("candidate_profiles");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User).WithOne(u => u.CandidateProfile).HasForeignKey<CandidateProfile>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.IsLookingForJob);
                entity.HasIndex(e => e.RecruiterSearchEnabled);
            });

            // Cấu hình bảng AdminProfile
            modelBuilder.Entity<AdminProfile>(entity =>
            {
                entity.ToTable("admin_profiles");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User).WithOne(u => u.AdminProfile).HasForeignKey<AdminProfile>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.RoleCode);
            });

            // Cấu hình bảng CompaniesProfile
            modelBuilder.Entity<CompanyProfile>(entity =>
            {
                entity.ToTable("companies_profile");
                entity.HasKey(e => e.Id);
                entity.HasIndex(c => c.CompanyName);
                entity.HasIndex(c => c.TaxCode).IsUnique();
                entity.Property(c => c.JobPostCreditsRemaining).HasDefaultValue(0);
            });

            // Cấu hình bảng CompanyMembers
            modelBuilder.Entity<CompanyMember>(entity =>
            {
                entity.ToTable("company_members");
                entity.HasKey(e => e.Id);
                entity.HasIndex(cm => cm.CompanyId);
                entity.HasIndex(cm => cm.UserId);
                entity.HasOne(cm => cm.CompanyProfile).WithMany(c => c.CompanyMembers).HasForeignKey(cm => cm.CompanyId);
                entity.HasOne(cm => cm.User).WithMany(u => u.CompanyMembers).HasForeignKey(cm => cm.UserId);
            });

            // ==========================================
            // CẤU HÌNH CÁC BẢNG MODULE TUYỂN DỤNG
            // ==========================================

            // Bảng JobCategories[cite: 27]
            modelBuilder.Entity<JobCategory>(entity =>
            {
                entity.ToTable("job_categories");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
            });

            // Bảng Skills[cite: 27]
            modelBuilder.Entity<Skill>(entity =>
            {
                entity.ToTable("skills");
                entity.HasKey(e => e.Id);
            });

            // Bảng Jobs[cite: 27]
            modelBuilder.Entity<Job>(entity =>
            {
                entity.ToTable("jobs");
                entity.HasKey(e => e.Id);

                // Quan hệ 1-N: Công ty -> Nhiều tin tuyển dụng
                entity.HasOne(j => j.Company)
                      .WithMany()
                      .HasForeignKey(j => j.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Quan hệ 1-N: Nhân viên -> Nhiều tin tuyển dụng
                entity.HasOne(j => j.CreatedByMember)
                      .WithMany()
                      .HasForeignKey(j => j.CreatedByMemberId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(j => j.Category)
                      .WithMany()
                      .HasForeignKey(j => j.CategoryId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Bảng JobProfiles[cite: 27]
            modelBuilder.Entity<JobProfile>(entity =>
            {
                entity.ToTable("job_profiles");
                entity.HasKey(e => e.Id);
                entity.Property(x => x.SalaryMin).HasPrecision(18, 2);
                entity.Property(x => x.SalaryMax).HasPrecision(18, 2);

                entity.HasOne(jp => jp.Job)
                      .WithOne(j => j.JobProfile)
                      .HasForeignKey<JobProfile>(jp => jp.JobId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ÉP KIỂU VECTOR CHO DB
                entity.Property(x => x.Embedding).HasColumnType("vector(3072)");
            });

            // Bảng trung gian JobRequiredSkills[cite: 27]
            modelBuilder.Entity<JobRequiredSkill>(entity =>
            {
                entity.ToTable("job_required_skills");
                entity.HasKey(e => e.Id);

                entity.HasOne(jrs => jrs.JobProfile)
                      .WithMany(jp => jp.RequiredSkills)
                      .HasForeignKey(jrs => jrs.JobProfileId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(jrs => jrs.Skill)
                      .WithMany()
                      .HasForeignKey(jrs => jrs.SkillId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
