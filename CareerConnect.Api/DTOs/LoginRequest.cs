using System.ComponentModel.DataAnnotations;

namespace CareerConnect.Api.DTOs
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email không được để trống.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        public string Password { get; set; } = string.Empty;
    }
}
