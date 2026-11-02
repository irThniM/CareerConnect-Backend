namespace CareerConnect.Application.Catalog.Requests;

public sealed class UpsertSkillRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();
}
