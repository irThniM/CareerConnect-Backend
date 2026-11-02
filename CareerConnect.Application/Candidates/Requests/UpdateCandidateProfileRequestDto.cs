using System.ComponentModel.DataAnnotations;

namespace CareerConnect.Application.Candidates.Requests
{
    public class UpdateCandidateProfileRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 120 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Số điện thoại không được vượt quá 30 ký tự.")]
        public string? PhoneNumber { get; set; }

        [StringLength(180, ErrorMessage = "Chức danh không được vượt quá 180 ký tự.")]
        public string? ProfessionalTitle { get; set; }

        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
        public string? Address { get; set; }

        [StringLength(700, ErrorMessage = "Giới thiệu không được vượt quá 700 ký tự.")]
        public string? Summary { get; set; }

        public bool IsLookingForJob { get; set; }
    }
}
