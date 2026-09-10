using CareerConnect.Application.Auth.Requests;
using CareerConnect.Application.Auth.Responses;
using CareerConnect.Application.Auth.Services;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using CareerConnect.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher _passwordHasher;
        private readonly JwtTokenService _jwtTokenService;

        public AuthService(AppDbContext context, PasswordHasher passwordHasher, JwtTokenService jwtTokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request)
        {
            // 1. Kiểm tra email đã tồn tại chưa
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                throw new Exception("Email này đã được sử dụng.");
            }

            // 2. Băm mật khẩu
            var hashedPassword = _passwordHasher.HashPassword(request.Password);

            // 3. Tạo User mới đi kèm với CandidateProfile rỗng
            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                AccountType = AccountType.Candidate,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                // Tận dụng Navigation Property tạo luôn Profile:
                CandidateProfile = new CandidateProfile
                {
                    FullName = request.Email.Split('@')[0], // Tạm lấy phần đầu của email làm tên hiển thị
                    IsLookingForJob = true,
                    RecruiterSearchEnabled = false
                }
            };

            _context.Users.Add(newUser); // Lưu 1 phát là nó tự rớt xuống cả 2 bảng Users và candidate_profiles
            await _context.SaveChangesAsync();

            // 4. Sinh Access Token và Refresh Token
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
            // 1. Tìm user theo email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                throw new Exception("Email hoặc mật khẩu không chính xác.");
            }

            // 2. Kiểm tra mật khẩu
            bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                throw new Exception("Email hoặc mật khẩu không chính xác.");
            }

            // 3. Sinh Access Token và Refresh Token
            var accessToken = _jwtTokenService.GenerateToken(user);
            var refreshToken = await CreateUserSessionAsync(user.Id);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                AccountType = user.AccountType.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
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