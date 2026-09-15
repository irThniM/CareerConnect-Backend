using CareerConnect.Application.Auth.Requests;
using CareerConnect.Application.Auth.Responses;
using CareerConnect.Application.Auth.Services;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using CareerConnect.Infrastructure.Auth;
using CareerConnect.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher _passwordHasher;
        private readonly JwtTokenService _jwtTokenService;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;

        public AuthService(AppDbContext context, PasswordHasher passwordHasher, JwtTokenService jwtTokenService, IEmailService emailService, IMemoryCache cache)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
            _emailService = emailService;
            _cache = cache; // Gán vào đây
        }

        public async Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null) throw new Exception("Email này đã được sử dụng.");

            // TẠO MÃ BÍ MẬT & LƯU TẠM VÀO CACHE (RAM) TRONG 15 PHÚT
            var verificationToken = Guid.NewGuid().ToString("N");
            _cache.Set(verificationToken, request, TimeSpan.FromMinutes(15));

            // GẮN TOKEN VÀO ĐƯỜNG LINK GỬI CHO KHÁCH
            string verifyLink = $"http://localhost:5173/verify-email?token={verificationToken}";

            string emailBody = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
            <h2 style='color: #2563eb; text-align: center;'>Chào mừng đến với CareerConnect!</h2>
            <p>Xin chào <strong>{request.FullName}</strong>,</p>
            <p>Vui lòng bấm vào nút bên dưới để kích hoạt tài khoản (Link có hiệu lực 15 phút):</p>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{verifyLink}' style='padding: 12px 24px; background-color: #00288e; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Xác thực Email ngay</a>
            </div>
        </div>";

            await _emailService.SendEmailAsync(request.Email, "[CareerConnect] Xác nhận đăng ký tài khoản", emailBody);

            return new AuthResponseDto { UserId = Guid.Empty, Email = request.Email };
        }


        public async Task<AuthResponseDto> RegisterEmployerAsync(RegisterEmployerRequestDto request)
        {
            // Sử dụng Database Transaction để đảm bảo tính toàn vẹn
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Kiểm tra email đã tồn tại chưa
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUser != null)
                {
                    throw new Exception("Email này đã được sử dụng.");
                }

                // 2. Tạo User trung tâm
                var hashedPassword = _passwordHasher.HashPassword(request.Password);
                var newUser = new User
                {
                    Email = request.Email,
                    PasswordHash = hashedPassword,
                    AccountType = AccountType.Employer,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // 3. Tạo Doanh nghiệp mới (Table: Companies)
                var newCompany = new Company
                {
                    CompanyName = request.CompanyName,
                    Industry = request.Industry,
                    CompanySize = request.CompanySize,
                    Address = $"{request.DetailedAddress}, {request.District}, {request.City}",
                    Website = request.Website,
                    ContactEmail = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Companies.Add(newCompany);
                await _context.SaveChangesAsync();

                // 4. Liên kết User với Company làm OWNER (Table: CompanyMembers)
                var companyMember = new CompanyMember
                {
                    CompanyId = newCompany.Id,
                    UserId = newUser.Id,
                    MemberRole = "OWNER",
                    Status = "ACTIVE",
                    FullName = request.ContactName,
                    ContactEmail = request.Email,
                    ZaloNumber = request.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CompanyMembers.Add(companyMember);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                // 5. Sinh Access Token và Refresh Token
                var accessToken = _jwtTokenService.GenerateToken(newUser);
                var refreshToken = await CreateUserSessionAsync(newUser.Id);

                return new AuthResponseDto
                {
                    UserId = newUser.Id,
                    Email = newUser.Email,
                    AccountType = newUser.AccountType.ToString(),
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            // 1. Tìm user kèm theo bảng CandidateProfile để lấy tên thật
            var user = await _context.Users
                .Include(u => u.CandidateProfile) // Nối bảng để lấy thông tin profile ứng viên
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                throw new Exception("Email hoặc mật khẩu không chính xác.");
            }

            if (user.Status != UserStatus.Active)
            {
                throw new Exception("Tài khoản chưa được kích hoạt.");
            }

            bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                throw new Exception("Email hoặc mật khẩu không chính xác.");
            }

            var accessToken = _jwtTokenService.GenerateToken(user);
            var refreshToken = await CreateUserSessionAsync(user.Id);

            // Trả về thêm FullName (nếu là Candidate thì lấy tên trong profile, ko thì để trống)
            string fullName = user.CandidateProfile?.FullName ?? user.Email;

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = fullName,
                AccountType = user.AccountType.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken,

            };
        }

        // HÀM NÀY ĐƯỢC CẬP NHẬT: KHI XÁC THỰC THÀNH CÔNG THÌ MỚI CHÍNH THỨC GHI DỮ LIỆU VÀO DATABASE
        public async Task<bool> VerifyEmailAsync(string token) // Đổi tham số thành chuỗi token
        {
            // Tìm token trong Cache
            if (!_cache.TryGetValue(token, out RegisterCandidateRequestDto? cachedRequest) || cachedRequest == null)
            {
                throw new Exception("Đường dẫn xác thực không hợp lệ hoặc đã hết hạn.");
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == cachedRequest.Email);
            if (existingUser != null) return true;

            // LÚC NÀY MỚI THỰC SỰ LƯU VÀO DATABASE
            var hashedPassword = _passwordHasher.HashPassword(cachedRequest.Password);
            var newUser = new User
            {
                Email = cachedRequest.Email,
                PasswordHash = hashedPassword,
                AccountType = AccountType.Candidate,
                Status = UserStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CandidateProfile = new CandidateProfile
                {
                    FullName = cachedRequest.FullName,
                    IsLookingForJob = true,
                    RecruiterSearchEnabled = false
                }
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Lưu thành công thì xóa token khỏi Cache để không ai click lại được nữa
            _cache.Remove(token);

            return true;
        }


        // Hàm hỗ trợ sinh và lưu Session vào bảng user_sessions
        private async Task<string> CreateUserSessionAsync(Guid userId)
        {
            var refreshToken = _jwtTokenService.GenerateRefreshTokenString();
            var hashedToken = _jwtTokenService.HashRefreshToken(refreshToken);

            var userSession = new UserSession
            {
                UserId = userId,
                RefreshTokenHash = hashedToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Refresh Token có hiệu lực 7 ngày
                CreatedAt = DateTime.UtcNow
            };

            _context.UserSessions.Add(userSession);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var hashedToken = _jwtTokenService.HashRefreshToken(request.RefreshToken);

            // Tìm session hợp lệ trong DB
            var session = await _context.UserSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.RefreshTokenHash == hashedToken && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow);

            if (session == null)
            {
                throw new Exception("Refresh token không hợp lệ hoặc đã hết hạn.");
            }

            var newAccessToken = _jwtTokenService.GenerateToken(session.User);

            return new AuthResponseDto
            {
                UserId = session.User.Id,
                Email = session.User.Email,
                AccountType = session.User.AccountType.ToString(),
                AccessToken = newAccessToken,
                RefreshToken = request.RefreshToken
            };
        }
    }
}