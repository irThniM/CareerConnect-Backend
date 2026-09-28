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
            _cache = cache;
        }

        public async Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null) throw new Exception("Email này đã được sử dụng.");

            // TẠO MÃ OTP 6 SỐ NGẪU NHIÊN VÀ LƯU CACHE
            string otpCode = new Random().Next(100000, 999999).ToString();

            _cache.Set($"CandidateReg_{request.Email}", request, TimeSpan.FromMinutes(15));
            _cache.Set($"CandidateOtp_{request.Email}", otpCode, TimeSpan.FromMinutes(15));

            // GỌI API BREVO ĐỂ GỬI MAIL THEO TEMPLATE (Truyền đúng OtpCode)
            await _emailService.SendEmailWithTemplateAsync(
                toEmail: request.Email,
                templateId: 1, // XEM LƯU Ý BÊN DƯỚI ĐỂ THAY SỐ NÀY BẰNG ID TEMPLATE BREVO CỦA BÁC
                parameters: new
                {
                    FullName = request.FullName,
                    OtpCode = otpCode
                }
            );

            return new AuthResponseDto { UserId = Guid.Empty, Email = request.Email };
        }


        // 1. HÀM NÀY CHỈ LƯU CACHE VÀ BẮN MAIL (TUYỆT ĐỐI KHÔNG LƯU DATABASE)
        public async Task<AuthResponseDto> RegisterEmployerAsync(RegisterEmployerRequestDto request)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null) throw new Exception("Email này đã được sử dụng.");

            // Tạo mã OTP 6 số ngẫu nhiên
            string otpCode = new Random().Next(100000, 999999).ToString();

            // Lưu Thông tin form & OTP vào Cache 15 phút
            _cache.Set($"EmployerReg_{request.Email}", request, TimeSpan.FromMinutes(15));
            _cache.Set($"EmployerOtp_{request.Email}", otpCode, TimeSpan.FromMinutes(15));

            // GỌI API BREVO ĐỂ GỬI MAIL THEO TEMPLATE
            await _emailService.SendEmailWithTemplateAsync(
                toEmail: request.Email,
                templateId: 1,
                parameters: new
                {
                    ContactName = request.ContactName,
                    CompanyName = request.CompanyName,
                    OtpCode = otpCode
                }
            );

            // Trả về email để Frontend biết là thành công, KHÔNG TRẢ VỀ TOKEN
            return new AuthResponseDto { UserId = Guid.Empty, Email = request.Email };
        }


        // 2. HÀM NÀY KIỂM TRA OTP VÀ MỚI THỰC SỰ LƯU VÀO DATABASE
        public async Task<bool> VerifyEmployerOtpAsync(VerifyEmployerOtpRequestDto request)
        {
            // Kiểm tra OTP
            if (!_cache.TryGetValue($"EmployerOtp_{request.Email}", out string? cachedOtp) || cachedOtp != request.Otp)
            {
                throw new Exception("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            // Lấy lại dữ liệu người dùng đã nhập ở Form
            if (!_cache.TryGetValue($"EmployerReg_{request.Email}", out RegisterEmployerRequestDto? cachedRegRequest) || cachedRegRequest == null)
            {
                throw new Exception("Thông tin đăng ký đã hết hạn, vui lòng đăng ký lại từ đầu.");
            }

            // BẮT ĐẦU LƯU DATABASE
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hashedPassword = _passwordHasher.HashPassword(cachedRegRequest.Password);

                // 1. LƯU USER
                var newUser = new User
                {
                    Email = cachedRegRequest.Email,
                    PasswordHash = hashedPassword,
                    AccountType = AccountType.Employer,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // 2. LƯU COMPANY
                var newCompany = new CompanyProfile
                {
                    CompanyName = cachedRegRequest.CompanyName,
                    TaxCode = string.IsNullOrWhiteSpace(cachedRegRequest.TaxCode) ? null : cachedRegRequest.TaxCode.Trim(),
                    TaxStatus = string.IsNullOrWhiteSpace(cachedRegRequest.TaxCode) ? null : cachedRegRequest.TaxStatus,
                    Industry = cachedRegRequest.Industry,
                    CompanySize = cachedRegRequest.CompanySize,
                    Address = $"{cachedRegRequest.DetailedAddress}, {cachedRegRequest.District}, {cachedRegRequest.City}",
                    Website = cachedRegRequest.Website,
                    ContactEmail = cachedRegRequest.Email,
                    PhoneNumber = cachedRegRequest.PhoneNumber,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CompanyProfiles.Add(newCompany);
                await _context.SaveChangesAsync();

                // 3. LƯU COMPANY MEMBER
                var companyMember = new CompanyMember
                {
                    CompanyId = newCompany.Id,
                    UserId = newUser.Id,
                    MemberRole = "OWNER",
                    Status = "PENDING",
                    FullName = cachedRegRequest.ContactName,
                    ContactEmail = cachedRegRequest.Email,
                    ZaloNumber = cachedRegRequest.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CompanyMembers.Add(companyMember);
                await _context.SaveChangesAsync();

                // 4. COMMIT & XÓA CACHE
                await transaction.CommitAsync();
                _cache.Remove($"EmployerOtp_{request.Email}");
                _cache.Remove($"EmployerReg_{request.Email}");

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _context.Users
                .Include(u => u.CandidateProfile)
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

            string fullName = user.Email.Split('@')[0];

            if (user.AccountType == AccountType.Candidate && user.CandidateProfile != null)
            {
                fullName = user.CandidateProfile.FullName;
            }
            else if (user.AccountType == AccountType.Employer)
            {
                var employerProfile = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.UserId == user.Id);

                if (employerProfile != null && !string.IsNullOrEmpty(employerProfile.FullName))
                {
                    fullName = employerProfile.FullName;
                }
            }

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = fullName,
                AccountType = user.AccountType.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        // HÀM NÀY ĐƯỢC CẬP NHẬT: KIỂM TRA MÃ OTP THAY VÌ TOKEN LINK
        public async Task<bool> VerifyEmailAsync(VerifyEmailRequestDto request)
        {
            // Kiểm tra OTP
            if (!_cache.TryGetValue($"CandidateOtp_{request.Email}", out string? cachedOtp) || cachedOtp != request.Otp)
            {
                throw new Exception("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            // Tìm thông tin đăng ký trong Cache
            if (!_cache.TryGetValue($"CandidateReg_{request.Email}", out RegisterCandidateRequestDto? cachedRequest) || cachedRequest == null)
            {
                throw new Exception("Thông tin đăng ký đã hết hạn, vui lòng đăng ký lại.");
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

            // Lưu thành công thì xóa dữ liệu khỏi Cache
            _cache.Remove($"CandidateOtp_{request.Email}");
            _cache.Remove($"CandidateReg_{request.Email}");

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
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserSessions.Add(userSession);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var hashedToken = _jwtTokenService.HashRefreshToken(request.RefreshToken);

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