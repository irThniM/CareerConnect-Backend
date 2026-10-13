namespace CareerConnect.Application.Jobs.Responses
{
    public class JobCompanyDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
    }
}
