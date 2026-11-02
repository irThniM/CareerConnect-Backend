namespace CareerConnect.Application.ProfessionalDevelopment.Models;

/// <summary>Optional outline-only event contract; this module is not in entity design v1.2.</summary>
public sealed class CareerEventDraft
{
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public string? Location { get; set; }
}
