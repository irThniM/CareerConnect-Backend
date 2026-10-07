namespace CareerConnect.Application.Admin.Responses
{
    public class CompanyManagementDto
    {
        public long Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? ContactEmail { get; set; }
        public string Status { get; set; } = string.Empty; // PENDING, ACTIVE, BANNED
        public DateTime CreatedAt { get; set; }
        public string? LicensePdfUrl { get; set; } // Link PDF để admin bấm vào xem
    }
}