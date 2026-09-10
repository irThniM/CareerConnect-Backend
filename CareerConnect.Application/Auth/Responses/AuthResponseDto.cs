namespace CareerConnect.Application.Auth.Responses
{
    public class AuthResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;

        // Chuỗi Token để Frontend đính kèm vào mỗi request sau này
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
