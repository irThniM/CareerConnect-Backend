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
        public DbSet<Resume> Resumes { get; set; }
        public DbSet<CandidateSkill> CandidateSkills { get; set; }
        public DbSet<CompanyProfile> CompanyProfiles { get; set; }
        public DbSet<CompanyMember> CompanyMembers { get; set; }
        public DbSet<AdminProfile> AdminProfiles { get; set; }

        public DbSet<JobCategory> JobCategories { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobProfile> JobProfiles { get; set; }
        public DbSet<JobRequiredSkill> JobRequiredSkills { get; set; }
        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<ApplicationStatusHistory> ApplicationStatusHistories { get; set; }
        public DbSet<InterviewSchedule> InterviewSchedules { get; set; }
        public DbSet<Package> Packages { get; set; }
        public DbSet<CompanySubscription> CompanySubscriptions { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

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
                entity.Property(e => e.Embedding).HasColumnType("vector(3072)");
            });

            modelBuilder.Entity<Resume>(entity =>
            {
                entity.ToTable("resumes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CandidateProfileId).IsUnique();
                entity.Property(e => e.SourceType).HasDefaultValue("UPLOADED");
                entity.Property(e => e.FileUrl).IsRequired();
                entity.Property(e => e.CloudinaryPublicId).IsRequired();
                entity.Property(e => e.OriginalFileName).IsRequired();
                entity.Property(e => e.ContentType).IsRequired();
                entity.Property(e => e.ProcessingStatus).HasDefaultValue("PENDING");
                entity.Property(e => e.StructuredData).HasColumnType("jsonb");
                entity.HasOne(e => e.CandidateProfile)
                    .WithOne(e => e.Resume)
                    .HasForeignKey<Resume>(e => e.CandidateProfileId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CandidateSkill>(entity =>
            {
                entity.ToTable("candidate_skills");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SourceType).HasDefaultValue("CV");
                entity.HasIndex(e => new { e.CandidateProfileId, e.SkillId }).IsUnique();
                entity.HasOne(e => e.CandidateProfile)
                    .WithMany(e => e.Skills)
                    .HasForeignKey(e => e.CandidateProfileId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Skill)
                    .WithMany()
                    .HasForeignKey(e => e.SkillId)
                    .OnDelete(DeleteBehavior.Restrict);
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

            modelBuilder.Entity<JobApplication>(entity =>
            {
                entity.ToTable("job_applications");
                entity.HasKey(application => application.Id);
                entity.Property(application => application.SourceType).HasMaxLength(40).IsRequired();
                entity.Property(application => application.Status).HasMaxLength(40).IsRequired();
                entity.Property(application => application.CoverLetter).HasMaxLength(2000);
                entity.Property(application => application.ResumeCloudinaryPublicIdSnapshot).HasMaxLength(512).IsRequired();
                entity.Property(application => application.ResumeFileNameSnapshot).HasMaxLength(255).IsRequired();
                entity.Property(application => application.ResumeContentTypeSnapshot).HasMaxLength(120).IsRequired();
                entity.HasIndex(application => new { application.JobId, application.CandidateProfileId })
                    .IsUnique()
                    .HasDatabaseName(DatabaseConstraintErrors.JobApplicationUniqueIndex);
                entity.HasIndex(application => new { application.CandidateProfileId, application.AppliedAt });
                entity.HasOne(application => application.Job)
                    .WithMany()
                    .HasForeignKey(application => application.JobId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(application => application.CandidateProfile)
                    .WithMany()
                    .HasForeignKey(application => application.CandidateProfileId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(application => application.Resume)
                    .WithMany()
                    .HasForeignKey(application => application.ResumeId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Ignore(application => application.Invitation);
                entity.Ignore(application => application.Conversation);
            });

            modelBuilder.Entity<InterviewSchedule>(entity =>
            {
                entity.ToTable("interview_schedules");
                entity.HasKey(interview => interview.Id);
                entity.Property(interview => interview.Status).HasMaxLength(24).IsRequired();
                entity.Property(interview => interview.Note).HasMaxLength(1000);
                entity.Property(interview => interview.ResponseNote).HasMaxLength(1000);
                entity.HasIndex(interview => new { interview.ApplicationId, interview.ScheduledStart });
                entity.HasOne(interview => interview.Application)
                    .WithMany(application => application.Interviews)
                    .HasForeignKey(interview => interview.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(interview => interview.ProposedByUser)
                    .WithMany()
                    .HasForeignKey(interview => interview.ProposedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ApplicationStatusHistory>(entity =>
            {
                entity.ToTable("application_status_histories");
                entity.HasKey(history => history.Id);
                entity.Property(history => history.OldStatus).HasMaxLength(40);
                entity.Property(history => history.NewStatus).HasMaxLength(40).IsRequired();
                entity.Property(history => history.Note).HasMaxLength(1000);
                entity.HasIndex(history => new { history.ApplicationId, history.CreatedAt });
                entity.HasOne(history => history.Application)
                    .WithMany(application => application.StatusHistory)
                    .HasForeignKey(history => history.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(history => history.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(history => history.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Package>(entity =>
            {
                entity.ToTable("packages");
                entity.HasKey(package => package.Id);
                entity.Property(package => package.Name).HasMaxLength(120).IsRequired();
                entity.Property(package => package.Description).HasMaxLength(1000);
                entity.Property(package => package.Price).HasPrecision(18, 2);
                entity.HasIndex(package => package.IsActive);
            });

            modelBuilder.Entity<CompanySubscription>(entity =>
            {
                entity.ToTable("company_subscriptions");
                entity.HasKey(subscription => subscription.Id);
                entity.Property(subscription => subscription.Status).HasMaxLength(24).IsRequired();
                entity.HasIndex(subscription => new { subscription.CompanyId, subscription.Status, subscription.EndAt });
                entity.HasOne(subscription => subscription.Company)
                    .WithMany()
                    .HasForeignKey(subscription => subscription.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(subscription => subscription.Package)
                    .WithMany()
                    .HasForeignKey(subscription => subscription.PackageId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.ToTable("payment_transactions");
                entity.HasKey(payment => payment.Id);
                entity.Property(payment => payment.Provider).HasMaxLength(24).IsRequired();
                entity.Property(payment => payment.TransactionRef).HasMaxLength(100).IsRequired();
                entity.Property(payment => payment.Amount).HasPrecision(18, 2);
                entity.Property(payment => payment.Currency).HasMaxLength(8).IsRequired();
                entity.Property(payment => payment.Status).HasMaxLength(24).IsRequired();
                entity.Property(payment => payment.ProviderTransactionNo).HasMaxLength(100);
                entity.Property(payment => payment.ResponseCode).HasMaxLength(16);
                entity.Property(payment => payment.RawReturnDataJson).HasColumnType("jsonb");
                entity.HasIndex(payment => payment.TransactionRef).IsUnique();
                entity.HasIndex(payment => new { payment.CompanyId, payment.CreatedAt });
                entity.HasOne(payment => payment.Company)
                    .WithMany()
                    .HasForeignKey(payment => payment.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(payment => payment.Subscription)
                    .WithMany()
                    .HasForeignKey(payment => payment.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
