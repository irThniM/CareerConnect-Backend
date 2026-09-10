namespace CareerConnect.Application.Auth.Requests
{
    public class RegisterEmployerRequestDto
    {
public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        
        // Thêm 2 dòng này vào DTO nếu đang bị thiếu nè:
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        
        public string DetailedAddress { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string CompanySize { get; set; } = string.Empty;
        public string? Website { get; set; }
    }
}
