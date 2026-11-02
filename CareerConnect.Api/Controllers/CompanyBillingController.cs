using System.Security.Claims;
using CareerConnect.Application.Billing.Requests;
using CareerConnect.Application.Billing.Responses;
using CareerConnect.Application.Billing.Services;
using CareerConnect.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareerConnect.Api.Controllers;

[ApiController]
[Route("api/company/billing")]
[Authorize]
public sealed class CompanyBillingController : ControllerBase
{
    private readonly ICompanyBillingService _billingService;
    private readonly ILogger<CompanyBillingController> _logger;

    public CompanyBillingController(ICompanyBillingService billingService, ILogger<CompanyBillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages(CancellationToken cancellationToken)
    {
        var accessError = ValidateEmployer(out _);
        if (accessError is not null) return accessError;
        return Ok(await _billingService.GetActivePackagesAsync(cancellationToken));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentSubscription(CancellationToken cancellationToken)
    {
        var accessError = ValidateEmployer(out var userId);
        if (accessError is not null) return accessError;

        try
        {
            return Ok(await _billingService.GetCurrentSubscriptionAsync(userId, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCompanyCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateEmployer(out var userId);
        if (accessError is not null) return accessError;
        if (request.PackageId <= 0) return BadRequest(new { message = "Vui lòng chọn gói dịch vụ hợp lệ." });

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var checkout = await _billingService.CreateSandboxCheckoutAsync(
                userId,
                request.PackageId,
                ipAddress,
                cancellationToken);
            return Ok(checkout);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create VNPay Sandbox checkout for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Chưa tạo được phiên thanh toán. Vui lòng thử lại." });
        }
    }

    [HttpGet("payments/{transactionRef}")]
    public async Task<IActionResult> GetPaymentStatus(string transactionRef, CancellationToken cancellationToken)
    {
        var accessError = ValidateEmployer(out var userId);
        if (accessError is not null) return accessError;

        try
        {
            var payment = await _billingService.GetPaymentStatusAsync(userId, transactionRef, cancellationToken);
            return payment is null ? NotFound(new { message = "Không tìm thấy giao dịch." }) : Ok(payment);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    // VNPay gọi endpoint này trực tiếp từ máy chủ của họ; không dùng JWT.
    [AllowAnonymous]
    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> VnpayIpn(CancellationToken cancellationToken)
    {
        var fields = Request.Query
            .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        try
        {
            return Ok(await _billingService.ProcessSandboxIpnAsync(fields, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "VNPay IPN could not be processed because payment configuration is incomplete.");
            return Ok(new VnpayIpnResponseDto { RspCode = "99", Message = "Configuration error" });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to process VNPay IPN notification.");
            return Ok(new VnpayIpnResponseDto { RspCode = "99", Message = "Unknown error" });
        }
    }

    private IActionResult? ValidateEmployer(out Guid userId)
    {
        userId = Guid.Empty;
        var accountType = User.FindFirst("account_type")?.Value ?? User.FindFirst("AccountType")?.Value;
        if (accountType != AccountType.Employer.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ tài khoản nhà tuyển dụng mới sử dụng được chức năng thanh toán doanh nghiệp." });
        }

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("UserId")?.Value;
        return Guid.TryParse(claim, out userId)
            ? null
            : Unauthorized(new { message = "Không thể xác định tài khoản. Vui lòng đăng nhập lại." });
    }
}
