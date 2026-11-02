namespace CareerConnect.Application.Billing.Responses;

public sealed class CompanyCheckoutDto
{
    public string TransactionRef { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
}
