using CareerConnect.Application.Admin.Requests;
using CareerConnect.Application.Admin.Responses;

namespace CareerConnect.Application.Admin.Services
{
    public interface IAdminService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<List<UserManagementDto>> GetAllUsersAsync();
        Task<List<CompanyManagementDto>> GetAllCompaniesAsync();
        Task<AdminProfileDto?> GetAdminProfileAsync(Guid userId);
        Task<AdminProfileDto?> UpdateAdminProfileAsync(Guid userId, UpdateAdminProfileRequestDto request);
        Task<List<AdminPackageDto>> GetAllPackagesAsync();
        Task<AdminPackageDto> CreatePackageAsync(SaveAdminPackageRequestDto request);
        Task<AdminPackageDto?> UpdatePackageAsync(long packageId, SaveAdminPackageRequestDto request);
        Task<AdminPackageDeleteResultDto?> DeletePackageAsync(long packageId);
    }
}
