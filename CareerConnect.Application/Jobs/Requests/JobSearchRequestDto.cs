namespace CareerConnect.Application.Jobs.Requests
{
    public class JobSearchRequestDto
    {
        public string? Keyword { get; set; }
        public string? Location { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkMode { get; set; }
        public string? Industry { get; set; }
        public string? Experience { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public long? CategoryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
