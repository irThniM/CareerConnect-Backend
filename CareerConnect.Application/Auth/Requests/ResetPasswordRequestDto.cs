namespace CareerConnect.Application.Auth.Requests;

public sealed class ResetPasswordRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
