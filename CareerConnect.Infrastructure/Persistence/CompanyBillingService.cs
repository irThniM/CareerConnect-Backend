using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerConnect.Application.Billing.Responses;
using CareerConnect.Application.Billing.Services;
using CareerConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CareerConnect.Infrastructure.Persistence;

public sealed class CompanyBillingService : ICompanyBillingService
{
    private const string VnpayProvider = "VNPAY";
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public CompanyBillingService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<CompanyPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Packages
            .AsNoTracking()
            .Where(package => package.IsActive)
            .OrderBy(package => package.Price)
            .Select(package => new CompanyPackageDto
            {
                Id = package.Id,
                Name = package.Name,
                Description = package.Description,
                Price = package.Price,
                DurationDays = package.DurationDays,
                MemberLimit = package.MemberLimit,
                JobPostLimit = package.JobPostLimit
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyCheckoutDto> CreateSandboxCheckoutAsync(
        Guid companyUserId,
        long packageId,
        string clientIpAddress,
        CancellationToken cancellationToken = default)
    {
        var company = await GetActiveCompanyForUserAsync(companyUserId, cancellationToken);
        var package = await _context.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == packageId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Gói dịch vụ không tồn tại hoặc đã ngừng bán.");

        if (package.Price < 5_000m)
        {
            throw new InvalidOperationException("Số tiền thanh toán Sandbox tối thiểu là 5.000 VND.");
        }
        if (package.Price != decimal.Truncate(package.Price) || package.DurationDays <= 0
            || package.JobPostLimit is <= 0 || package.MemberLimit is <= 0)
        {
            throw new InvalidOperationException("Cấu hình gói dịch vụ không hợp lệ. Hãy kiểm tra giá, thời hạn và các hạn mức.");
        }

        var returnUrl = RequiredSetting("Vnpay:ReturnUrl");
        var tmnCode = RequiredSetting("Vnpay:TmnCode");
        var hashSecret = RequiredSetting("Vnpay:HashSecret");
        var paymentUrl = _configuration["Vnpay:PaymentUrl"]
            ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        var now = DateTimeOffset.UtcNow;
        var transactionRef = $"CC{now:yyyyMMddHHmmss}{RandomNumberGenerator.GetInt32(100000, 999999)}";
        var subscription = new CompanySubscription
        {
            CompanyId = company.Id,
            PackageId = package.Id,
            Status = "PENDING",
            RemainingJobPosts = package.JobPostLimit,
            CreatedAt = now,
            UpdatedAt = now
        };
        var payment = new PaymentTransaction
        {
            CompanyId = company.Id,
            Subscription = subscription,
            Provider = VnpayProvider,
            TransactionRef = transactionRef,
            Amount = package.Price,
            Currency = "VND",
            Status = "PENDING",
            CreatedAt = now
        };

        await using (var databaseTransaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            _context.CompanySubscriptions.Add(subscription);
            _context.PaymentTransactions.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }

        var localNow = ConvertToVietnamTime(now);
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = decimal.ToInt64(package.Price * 100m).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = transactionRef,
            ["vnp_OrderInfo"] = $"CareerConnect package {package.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = AddTransactionRef(returnUrl, transactionRef),
            ["vnp_IpAddr"] = NormalizeIpAddress(clientIpAddress),
            ["vnp_CreateDate"] = localNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = localNow.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };

        var signData = BuildCanonicalData(parameters);
        parameters["vnp_SecureHash"] = ComputeHmacSha512(hashSecret, signData);
        var checkoutUrl = $"{paymentUrl}?{string.Join("&", parameters.Select(pair =>
            $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"))}";

        return new CompanyCheckoutDto { TransactionRef = transactionRef, CheckoutUrl = checkoutUrl };
    }

    public async Task<VnpayIpnResponseDto> ProcessSandboxIpnAsync(
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken = default)
    {
        var hashSecret = RequiredSetting("Vnpay:HashSecret");
        if (!fields.TryGetValue("vnp_SecureHash", out var suppliedHash) || string.IsNullOrWhiteSpace(suppliedHash))
        {
            return IpN("97", "Invalid checksum");
        }

        var signedFields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in fields)
        {
            if (pair.Key.StartsWith("vnp_", StringComparison.Ordinal)
                && pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
            {
                signedFields[pair.Key] = pair.Value;
            }
        }

        var expectedHash = ComputeHmacSha512(hashSecret, BuildCanonicalData(signedFields));
        if (!FixedTimeHexEquals(expectedHash, suppliedHash))
        {
            return IpN("97", "Invalid checksum");
        }

        if (!fields.TryGetValue("vnp_TxnRef", out var transactionRef)
            || !fields.TryGetValue("vnp_Amount", out var amountText)
            || !long.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out var returnedAmount))
        {
            return IpN("99", "Invalid payment data");
        }

        var payment = await _context.PaymentTransactions
            .Include(item => item.Subscription)
                .ThenInclude(subscription => subscription!.Package)
            .FirstOrDefaultAsync(item => item.TransactionRef == transactionRef && item.Provider == VnpayProvider, cancellationToken);

        if (payment is null) return IpN("01", "Order not found");
        if (payment.Status == "SUCCESS") return IpN("02", "Order already confirmed");

        var expectedAmount = decimal.ToInt64(payment.Amount * 100m);
        if (returnedAmount != expectedAmount) return IpN("04", "Invalid amount");
        if (payment.Subscription is null) return IpN("01", "Subscription not found");

        var now = DateTimeOffset.UtcNow;
        var responseCode = fields.GetValueOrDefault("vnp_ResponseCode", string.Empty);
        var transactionStatus = fields.GetValueOrDefault("vnp_TransactionStatus", string.Empty);
        var sanitizedCallback = fields
            .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal)
                && pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (responseCode == "00" && transactionStatus == "00")
        {
            var previousEnd = await _context.CompanySubscriptions
                .Where(subscription => subscription.CompanyId == payment.CompanyId
                    && subscription.Status == "ACTIVE"
                    && subscription.EndAt > now)
                .MaxAsync(subscription => (DateTimeOffset?)subscription.EndAt, cancellationToken);
            var startsAt = previousEnd.HasValue && previousEnd.Value > now ? previousEnd.Value : now;
            var package = payment.Subscription.Package;

            payment.Status = "SUCCESS";
            payment.Subscription.Status = "ACTIVE";
            payment.Subscription.StartAt = startsAt;
            payment.Subscription.EndAt = startsAt.AddDays(package.DurationDays);
            payment.Subscription.RemainingJobPosts = package.JobPostLimit;
            payment.Subscription.UpdatedAt = now;
            payment.CompletedAt = now;
            payment.ProviderTransactionNo = fields.GetValueOrDefault("vnp_TransactionNo");
            payment.ResponseCode = responseCode;
            payment.RawReturnDataJson = JsonSerializer.Serialize(sanitizedCallback);
            await _context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
            return IpN("00", "Confirm Success");
        }

        payment.Status = "FAILED";
        payment.Subscription.Status = "FAILED";
        payment.Subscription.UpdatedAt = now;
        payment.CompletedAt = now;
        payment.ProviderTransactionNo = fields.GetValueOrDefault("vnp_TransactionNo");
        payment.ResponseCode = responseCode;
        payment.RawReturnDataJson = JsonSerializer.Serialize(sanitizedCallback);
        await _context.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return IpN("00", "Confirm Success");
    }

    public async Task<CompanySubscriptionDto?> GetCurrentSubscriptionAsync(
        Guid companyUserId,
        CancellationToken cancellationToken = default)
    {
        var company = await GetActiveCompanyForUserAsync(companyUserId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return await _context.CompanySubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.CompanyId == company.Id
                && subscription.Status == "ACTIVE"
                && subscription.StartAt <= now
                && subscription.EndAt > now)
            .OrderByDescending(subscription => subscription.EndAt)
            .Select(subscription => new CompanySubscriptionDto
            {
                PackageName = subscription.Package.Name,
                Status = subscription.Status,
                StartAt = subscription.StartAt,
                EndAt = subscription.EndAt,
                RemainingJobPosts = subscription.RemainingJobPosts,
                MemberLimit = subscription.Package.MemberLimit
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompanyPaymentStatusDto?> GetPaymentStatusAsync(
        Guid companyUserId,
        string transactionRef,
        CancellationToken cancellationToken = default)
    {
        var company = await GetActiveCompanyForUserAsync(companyUserId, cancellationToken);
        return await _context.PaymentTransactions
            .AsNoTracking()
            .Where(payment => payment.CompanyId == company.Id && payment.TransactionRef == transactionRef)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(payment => new CompanyPaymentStatusDto
            {
                TransactionRef = payment.TransactionRef,
                Status = payment.Status,
                Amount = payment.Amount,
                PackageName = payment.Subscription == null ? "Gói dịch vụ" : payment.Subscription.Package.Name,
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CompanyProfile> GetActiveCompanyForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var member = await _context.CompanyMembers
            .Include(item => item.CompanyProfile)
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Status == "ACTIVE", cancellationToken);

        if (member is null) throw new InvalidOperationException("Tài khoản chưa được liên kết với doanh nghiệp đang hoạt động.");
        if (member.CompanyProfile.Status != "ACTIVE") throw new InvalidOperationException("Doanh nghiệp cần hoàn tất xác minh trước khi sử dụng gói dịch vụ.");
        return member.CompanyProfile;
    }

    private string RequiredSetting(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Thiếu cấu hình thanh toán {key}. Hãy thêm giá trị vào User Secrets của CareerConnect.Api.");
        }
        return value;
    }

    private static string BuildCanonicalData(IEnumerable<KeyValuePair<string, string>> fields)
    {
        return string.Join("&", fields
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
    }

    private static string ComputeHmacSha512(string secret, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToHexString(HMACSHA512.HashData(keyBytes, dataBytes)).ToLowerInvariant();
    }

    private static bool FixedTimeHexEquals(string expected, string actual)
    {
        try
        {
            var expectedBytes = Convert.FromHexString(expected);
            var actualBytes = Convert.FromHexString(actual);
            return expectedBytes.Length == actualBytes.Length
                && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeIpAddress(string value)
    {
        if (IPAddress.TryParse(value, out var address))
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
        }
        return "127.0.0.1";
    }

    private static string AddTransactionRef(string returnUrl, string transactionRef)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}transactionRef={WebUtility.UrlEncode(transactionRef)}";
    }

    private static DateTime ConvertToVietnamTime(DateTimeOffset instant)
    {
        var zoneId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
        return TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime;
    }

    private static VnpayIpnResponseDto IpN(string code, string message) => new() { RspCode = code, Message = message };
}
