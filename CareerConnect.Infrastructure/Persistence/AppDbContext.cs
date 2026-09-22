using CareerConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Đại diện cho các bảng trong cơ sở dữ liệu
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        public DbSet<CandidateProfile> CandidateProfiles { get; set; }

        public DbSet<CompanyProfile> CompanyProfiles { get; set; } 
        public DbSet<CompanyMember> CompanyMembers { get; set; }

        public DbSet<AdminProfile> AdminProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình các ràng buộc cho bảng users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email)
                      .IsRequired()
                      .HasMaxLength(255);
                entity.HasIndex(e => e.Email)
                      .IsUnique();

                // Ép EF Core lưu Enum thành chuỗi (VARCHAR) vào Database thay vì số nguyên (0, 1, 2)
                entity.Property(e => e.AccountType).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // cấu hình các ràng buộc cho bảng user_sessions
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.ToTable("user_sessions");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.RefreshTokenHash).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Sessions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Cấu hình bảng candidate_profiles
            modelBuilder.Entity<CandidateProfile>(entity =>
            {
                entity.ToTable("candidate_profiles");
                entity.HasKey(e => e.Id);

                // Thiết lập quan hệ 1-1 và UNIQUE constraint cho user_id
                entity.HasOne(e => e.User)
                      .WithOne(u => u.CandidateProfile)
                      .HasForeignKey<CandidateProfile>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.IsLookingForJob);
                entity.HasIndex(e => e.RecruiterSearchEnabled);
            });

            // Cấu hình bảng admin_profiles (MỚI THÊM)
            modelBuilder.Entity<AdminProfile>(entity =>
            {
                entity.ToTable("admin_profiles");
                entity.HasKey(e => e.Id);

                // Thiết lập quan hệ 1-1 với User
                entity.HasOne(e => e.User)
                      .WithOne(u => u.AdminProfile)
                      .HasForeignKey<AdminProfile>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Đánh Index cho RoleCode để tối ưu tốc độ phân quyền nội bộ (Super Admin, Moderator...)
                entity.HasIndex(e => e.RoleCode);
            });

            // Cấu hình Index và ràng buộc cho bảng Companies
            modelBuilder.Entity<CompanyProfile>(entity =>
            {
                entity.ToTable("companies_profile");
                entity.HasKey(e => e.Id);

                entity.HasIndex(c => c.CompanyName);
                entity.HasIndex(c => c.TaxCode)
                      .IsUnique();
            });

            // Cấu hình bảng CompanyMembers và các chỉ mục tối ưu hiệu năng
            modelBuilder.Entity<CompanyMember>(entity =>
            {
                entity.ToTable("company_members");
                entity.HasKey(e => e.Id);

                entity.HasIndex(cm => cm.CompanyId);
                entity.HasIndex(cm => cm.UserId);

                // Cấu hình quan hệ
                entity.HasOne(cm => cm.CompanyProfile)
                      .WithMany(c => c.CompanyMembers)
                      .HasForeignKey(cm => cm.CompanyId);

                entity.HasOne(cm => cm.User)
                      .WithMany(u => u.CompanyMembers)
                      .HasForeignKey(cm => cm.UserId);
            });
        }
    }
}