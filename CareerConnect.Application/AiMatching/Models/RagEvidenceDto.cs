namespace CareerConnect.Application.AiMatching.Models;

/// <summary>Retrieved source excerpts for a later grounded explanation; no Gemini call is made here.</summary>
public sealed class RagEvidenceDto
{
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}
