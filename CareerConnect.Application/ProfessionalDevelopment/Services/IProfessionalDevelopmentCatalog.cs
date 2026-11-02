using CareerConnect.Application.ProfessionalDevelopment.Models;

namespace CareerConnect.Application.ProfessionalDevelopment.Services;

/// <summary>Optional courses/events contract from the outline; no database entities are planned in v1.2.</summary>
public interface IProfessionalDevelopmentCatalog
{
    Task<IReadOnlyList<CourseSummaryDraft>> GetCoursesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CareerEventDraft>> GetEventsAsync(CancellationToken cancellationToken = default);
}
