namespace CareerConnect.Application.Admin.Responses
{
    public class DashboardStatsDto
    {
        public int TotalCandidates { get; set; }
        public int TotalCompanies { get; set; }
        public int PendingCompanies { get; set; }
        public int OpenJobs { get; set; }
    }
}