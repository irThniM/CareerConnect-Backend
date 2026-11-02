namespace CareerConnect.Application.Catalog.Responses;

public sealed class CatalogItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; }
}
