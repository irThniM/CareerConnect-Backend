using CareerConnect.Application.Auth.Requests;
using CareerConnect.Application.Auth.Responses;

namespace CareerConnect.Application.Auth.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
        Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request);
        Task<AuthResponseDto> RegisterEmployerAsync(RegisterEmployerRequestDto request);
        Task<bool> VerifyEmailAsync(string token);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    }
}
