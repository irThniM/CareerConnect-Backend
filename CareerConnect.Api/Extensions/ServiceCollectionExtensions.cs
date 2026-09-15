
using CareerConnect.Application.Auth.Services;
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

            services.AddScoped<IEmailService, EmailService>();
            services.AddMemoryCache();

            // 3. THÊM ĐOẠN NÀY ĐỂ MỞ CORS CHO FRONTEND (React - Vite cổng 5173)
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    policy =>
                    {
                        policy.WithOrigins("http://localhost:5173") // Cho phép React gọi sang
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
            });

            return services;
        }
    }
}
