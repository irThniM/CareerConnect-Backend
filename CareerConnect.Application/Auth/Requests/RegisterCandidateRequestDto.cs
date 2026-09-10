namespace CareerConnect.Application.Auth.Requests
{
    public class RegisterCandidateRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Thuộc tính này sẽ được dùng để tạo bản ghi bên bảng candidate_profiles[cite: 1]
        public string FullName { get; set; } = string.Empty;
    }
}
