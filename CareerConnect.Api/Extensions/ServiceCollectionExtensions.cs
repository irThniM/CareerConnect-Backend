using CareerConnect.Application.Auth.Services;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using CareerConnect.Infrastructure.Auth;
using CareerConnect.Infrastructure.Email;
using CareerConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Đăng ký Database (Neon / PostgreSQL)
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            // 2. Đăng ký các dịch vụ Authentication & Security vào DI Container
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<PasswordHasher>();
            services.AddScoped<JwtTokenService>();

            // THAY ĐỔI Ở ĐÂY: Đăng ký EmailService đi kèm với cấu hình HttpClient chuẩn
            services.AddHttpClient<IEmailService, EmailService>();

            services.AddMemoryCache();

            // 3. MỞ CORS CHO FRONTEND (React - Vite cổng 5173)
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    policy =>
                    {
                        policy.WithOrigins("http://localhost:5173")
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
            });

            return services;
        }
        public static async Task SeedDatabaseAsync(this WebApplication app)
        {
            // Tạo một scope độc lập để lấy các Service ra dùng
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<AppDbContext>();
                var config = services.GetRequiredService<IConfiguration>();
                var passwordHasher = services.GetRequiredService<PasswordHasher>();

                // Đọc từ appsettings.json hoặc User Secrets
                var adminEmail = config["AdminSettings:DefaultEmail"];
                var adminPassword = config["AdminSettings:DefaultPassword"];
                var adminFullName = config["AdminSettings:DefaultFullName"] ?? "System Admin";

                if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
                {
                    // Dùng AnyAsync thay vì gọi db sync
                    var adminExists = await context.Users.AnyAsync(u => u.AccountType == AccountType.Admin);

                    if (!adminExists)
                    {
                        var adminUser = new User
                        {
                            Id = Guid.NewGuid(),
                            Email = adminEmail,
                            PasswordHash = passwordHasher.HashPassword(adminPassword),
                            AccountType = AccountType.Admin,
                            Status = UserStatus.Active,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        context.Users.Add(adminUser);

                        var adminProfile = new AdminProfile
                        {
                            UserId = adminUser.Id,
                            FullName = adminFullName,
                            RoleCode = "SUPER_ADMIN",
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        context.AdminProfiles.Add(adminProfile);

                        await context.SaveChangesAsync();
                        Console.WriteLine("Đã khởi tạo thành công tài khoản Super Admin từ Secret!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi khởi tạo dữ liệu gốc: {ex.Message}");
            }
        }
    }
}