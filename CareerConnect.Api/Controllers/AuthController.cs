using CareerConnect.Application.Auth.Requests;
using CareerConnect.Application.Auth.Responses;
using CareerConnect.Application.Auth.Services;
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

        public class VerifyOtpRequestDto
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
        }

        [HttpPost("verify-otp/employer")]
        public async Task<IActionResult> VerifyEmployerOtp([FromBody] VerifyOtpRequestDto request)
        {
            try
            {
                await _authService.VerifyEmployerOtpAsync(request.Email, request.Otp);
                return Ok(new { message = "Xác thực thành công! Tài khoản đang chờ duyệt." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        public class VerifyEmailRequestDto
        {
            public string Token { get; set; } = string.Empty;
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            try
            {
                await _authService.VerifyEmailAsync(request.Token);
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
