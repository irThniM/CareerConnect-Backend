using System.ComponentModel.DataAnnotations;

namespace CareerConnect.Application.Admin.Requests;

public sealed class UpdateAdminProfileRequestDto
{
    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(32)]
    public string? PhoneNumber { get; set; }
}
