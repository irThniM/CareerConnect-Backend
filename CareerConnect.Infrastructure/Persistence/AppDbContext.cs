using CareerConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Đại diện cho các bảng trong cơ sở dữ liệu[cite: 1]
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        public DbSet<CandidateProfile> CandidateProfiles { get; set; }

        public DbSet<Company> Companies { get; set; }
        public DbSet<CompanyMember> CompanyMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình các ràng buộc cho bảng users[cite: 1]
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users"); // Đặt tên bảng là "users" chữ thường[cite: 1]
                entity.HasKey(e => e.Id); // Khóa chính[cite: 1]
                entity.Property(e => e.Email)
                      .IsRequired()
                      .HasMaxLength(255); // Giới hạn độ dài[cite: 1]
                entity.HasIndex(e => e.Email)
                      .IsUnique(); // Đảm bảo email không được đăng ký trùng[cite: 1]
            });

            // cấu hình các ràng buộc cho bảng user_sessions[cite: 1]
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.ToTable("user_sessions");
                entity.HasKey(e => e.Id);

                // Đánh Index cho userId và Unique cho refresh_token_hash theo tài liệu thiết kế[cite: 1]
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

                // Đánh Index theo tài liệu thiết kế[cite: 1]
                entity.HasIndex(e => e.IsLookingForJob);
                entity.HasIndex(e => e.RecruiterSearchEnabled);
            });

            // Cấu hình Index và ràng buộc cho bảng Companies
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("companies");
                entity.HasKey(e => e.Id);

                entity.HasIndex(c => c.CompanyName); // Tăng tốc độ tìm kiếm doanh nghiệp
                entity.HasIndex(c => c.TaxCode)
                      .IsUnique(); // Tránh trùng lặp mã số thuế
            });

            // Cấu hình bảng CompanyMembers và các chỉ mục tối ưu hiệu năng
            modelBuilder.Entity<CompanyMember>(entity =>
            {
                entity.ToTable("company_members");
                entity.HasKey(e => e.Id);

                // Đánh Index cho các khóa ngoại
                entity.HasIndex(cm => cm.CompanyId);
                entity.HasIndex(cm => cm.UserId);

                // Cấu hình quan hệ
                entity.HasOne(cm => cm.Company)
                      .WithMany(c => c.CompanyMembers)
                      .HasForeignKey(cm => cm.CompanyId);

                entity.HasOne(cm => cm.User)
                      .WithMany(u => u.CompanyMembers)
                      .HasForeignKey(cm => cm.UserId);
            });
        }
    }
}
