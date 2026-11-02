using CareerConnect.Application.Auth.Requests;

namespace CareerConnect.Application.Auth.Services;

/// <summary>Planned recovery/Google sign-in extension. Existing IAuthService remains unchanged.</summary>
public interface IAuthRecoveryService
{
    Task RequestPasswordResetAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<object> SignInWithGoogleAsync(string identityToken, CancellationToken cancellationToken = default);
}
