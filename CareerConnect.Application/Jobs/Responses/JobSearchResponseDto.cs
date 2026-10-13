namespace CareerConnect.Application.Jobs.Responses
{
    public class JobSearchResponseDto
    {
        public List<JobListItemDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
