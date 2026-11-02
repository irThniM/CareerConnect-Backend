namespace CareerConnect.Application.Billing.Responses;

public sealed class CompanyPaymentStatusDto
{
    public string TransactionRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
