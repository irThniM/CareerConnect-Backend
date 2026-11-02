namespace CareerConnect.Application.Applications.Requests;

/// <summary>A short optional note sent with the candidate's current CV.</summary>
public sealed class SubmitApplicationRequestDto
{
    public string? CoverLetter { get; set; }
}
