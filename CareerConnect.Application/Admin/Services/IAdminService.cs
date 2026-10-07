using CareerConnect.Application.Admin.Responses;

namespace CareerConnect.Application.Admin.Services
{
    public interface IAdminService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<List<UserManagementDto>> GetAllUsersAsync();
        Task<List<CompanyManagementDto>> GetAllCompaniesAsync();
    }
}