namespace CareerConnect.Domain.Entities;

/// <summary>Optional VNPay Sandbox transaction record; this does not charge real money.</summary>
public sealed class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long CompanyId { get; set; }
    public long? SubscriptionId { get; set; }
    public string Provider { get; set; } = "VNPAY";
    public string TransactionRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "PENDING";
    public string? ProviderTransactionNo { get; set; }
    public string? ResponseCode { get; set; }
    /// <summary>Sanitized JSON callback payload; never store secrets or raw payment credentials.</summary>
    public string? RawReturnDataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public CompanyProfile Company { get; set; } = null!;
    public CompanySubscription? Subscription { get; set; }
}
