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

            // GỌI API BREVO ĐỂ GỬI MAIL THEO TEMPLATE (Giả sử ID là 1)
            await _emailService.SendEmailWithTemplateAsync(
                toEmail: request.Email,
                templateId: 1, // XEM LƯU Ý BÊN DƯỚI ĐỂ THAY SỐ NÀY
                parameters: new
                {
                    FullName = request.FullName, // Brevo sẽ nhận bằng {{params.FullName}}
                    VerifyLink = verifyLink      // Brevo sẽ nhận bằng {{params.VerifyLink}}
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

            // GỌI API BREVO ĐỂ GỬI MAIL THEO TEMPLATE (Giả sử ID là 2)
            await _emailService.SendEmailWithTemplateAsync(
                toEmail: request.Email,
                templateId: 1, // XEM LƯU Ý BÊN DƯỚI ĐỂ THAY SỐ NÀY
                parameters: new
                {
                    ContactName = request.ContactName, // Brevo sẽ nhận bằng {{params.ContactName}}
                    CompanyName = request.CompanyName, // Brevo sẽ nhận bằng {{params.CompanyName}}
                    OtpCode = otpCode                  // Brevo sẽ nhận bằng {{params.OtpCode}}
                }
            );

            // Trả về email để Frontend biết là thành công, KHÔNG TRẢ VỀ TOKEN
            return new AuthResponseDto { UserId = Guid.Empty, Email = request.Email };
        }


        // 2. HÀM NÀY KIỂM TRA OTP VÀ MỚI THỰC SỰ LƯU VÀO DATABASE
        public async Task<bool> VerifyEmployerOtpAsync(string email, string otp)
        {
            // Kiểm tra OTP
            if (!_cache.TryGetValue($"EmployerOtp_{email}", out string? cachedOtp) || cachedOtp != otp)
            {
                throw new Exception("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            // Lấy lại dữ liệu người dùng đã nhập ở Form
            if (!_cache.TryGetValue($"EmployerReg_{email}", out RegisterEmployerRequestDto? request) || request == null)
            {
                throw new Exception("Thông tin đăng ký đã hết hạn, vui lòng đăng ký lại từ đầu.");
            }

            // BẮT ĐẦU LƯU DATABASE
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hashedPassword = _passwordHasher.HashPassword(request.Password);

                // 1. LƯU USER
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

                // 2. LƯU COMPANY
                var newCompany = new CompanyProfile
                {
                    CompanyName = request.CompanyName,
                    TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim(),
                    TaxStatus = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxStatus,
                    Industry = request.Industry,
                    CompanySize = request.CompanySize,
                    Address = $"{request.DetailedAddress}, {request.District}, {request.City}",
                    Website = request.Website,
                    ContactEmail = request.Email,
                    PhoneNumber = request.PhoneNumber,
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
                    FullName = request.ContactName,
                    ContactEmail = request.Email,
                    ZaloNumber = request.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CompanyMembers.Add(companyMember);
                await _context.SaveChangesAsync();

                // 4. COMMIT & XÓA CACHE
                await transaction.CommitAsync();
                _cache.Remove($"EmployerOtp_{email}");
                _cache.Remove($"EmployerReg_{email}");

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
            // 1. Tìm user, chỉ Include CandidateProfile thôi
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

            // 2. Logic lấy tên hiển thị 
            string fullName = user.Email.Split('@')[0]; // Tên mặc định

            if (user.AccountType == AccountType.Candidate && user.CandidateProfile != null)
            {
                fullName = user.CandidateProfile.FullName;
            }
            else if (user.AccountType == AccountType.Employer)
            {
                // Tự động chọc thẳng vào bảng CompanyMembers tìm thông tin theo UserId
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