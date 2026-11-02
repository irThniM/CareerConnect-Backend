using CareerConnect.Application.Billing.Responses;

namespace CareerConnect.Application.Billing.Services;

public interface ICompanyBillingService
{
    Task<IReadOnlyList<CompanyPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<CompanyCheckoutDto> CreateSandboxCheckoutAsync(
        Guid companyUserId,
        long packageId,
        string clientIpAddress,
        CancellationToken cancellationToken = default);
    Task<VnpayIpnResponseDto> ProcessSandboxIpnAsync(
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken = default);
    Task<CompanySubscriptionDto?> GetCurrentSubscriptionAsync(Guid companyUserId, CancellationToken cancellationToken = default);
    Task<CompanyPaymentStatusDto?> GetPaymentStatusAsync(
        Guid companyUserId,
        string transactionRef,
        CancellationToken cancellationToken = default);
}
