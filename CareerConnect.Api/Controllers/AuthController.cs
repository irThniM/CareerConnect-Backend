using CareerConnect.Application.Auth.Requests;
using CareerConnect.Application.Auth.Responses;
using CareerConnect.Application.Auth.Services;
using CareerConnect.Domain.Enums;
using CareerConnect.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareerConnect.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register/candidate")]
        public async Task<ActionResult<AuthResponseDto>> RegisterCandidate([FromBody] RegisterCandidateRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterCandidateAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register/employer")]
        public async Task<IActionResult> RegisterEmployer([FromBody] RegisterEmployerRequestDto request)
        {
            try
            {
                await _authService.RegisterEmployerAsync(request);
                return Ok(new { message = "Đã gửi mã OTP. Vui lòng kiểm tra email." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("verify-otp/employer")]
        public async Task<IActionResult> VerifyEmployerOtp([FromBody] VerifyEmployerOtpRequestDto request)
        {
            try
            {
                // Truyền thẳng cục request DTO vào Service
                await _authService.VerifyEmployerOtpAsync(request);
                return Ok(new { message = "Xác thực thành công! Tài khoản đang chờ duyệt." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login/candidate")]
        public async Task<ActionResult<AuthResponseDto>> CandidateLogin([FromBody] LoginRequestDto request)
        {
            return await LoginForAccountType(request, AccountType.Candidate);
        }

        [HttpPost("login/employer")]
        public async Task<ActionResult<AuthResponseDto>> EmployerLogin([FromBody] LoginRequestDto request)
        {
            return await LoginForAccountType(request, AccountType.Employer);
        }

        [HttpPost("login/admin")]
        public async Task<ActionResult<AuthResponseDto>> AdminLogin([FromBody] LoginRequestDto request)
        {
            return await LoginForAccountType(request, AccountType.Admin);
        }

        private async Task<ActionResult<AuthResponseDto>> LoginForAccountType(
            LoginRequestDto request,
            AccountType expectedAccountType)
        {
            try
            {
                var result = await _authService.LoginAsync(request, expectedAccountType);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            try
            {
                // Truyền thẳng cục request DTO vào Service
                await _authService.VerifyEmailAsync(request);
                return Ok(new { message = "Kích hoạt tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
