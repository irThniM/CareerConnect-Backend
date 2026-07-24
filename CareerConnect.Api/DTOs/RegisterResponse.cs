namespace CareerConnect.Api.DTOs
{
    public class RegisterResponse
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

    }
}
