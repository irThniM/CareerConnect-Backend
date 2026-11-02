using CareerConnect.Application.Catalog.Requests;
using CareerConnect.Application.Catalog.Responses;

namespace CareerConnect.Application.Catalog.Services;

public interface IJobCatalogAdminService
{
    Task<IReadOnlyList<CatalogItemDto>> GetJobCategoriesAsync(CancellationToken cancellationToken = default);
    Task<long> UpsertJobCategoryAsync(string name, string slug, string? description, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogItemDto>> GetSkillsAsync(CancellationToken cancellationToken = default);
    Task<long> UpsertSkillAsync(UpsertSkillRequestDto request, CancellationToken cancellationToken = default);
    Task SetSkillActiveAsync(long skillId, bool isActive, CancellationToken cancellationToken = default);
}
