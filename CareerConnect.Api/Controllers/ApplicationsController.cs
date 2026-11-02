using CareerConnect.Application.Applications.Exceptions;
using CareerConnect.Application.Applications.Requests;
using CareerConnect.Application.Applications.Services;
using CareerConnect.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CareerConnect.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    [HttpPost("jobs/{jobId:long}")]
    public async Task<IActionResult> Submit(
        long jobId,
        [FromBody] SubmitApplicationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(AccountType.Candidate, out var userId, out var accessError)) return accessError!;

        try
        {
            var applicationId = await _applicationService.SubmitAsync(userId, jobId, request, cancellationToken);
            return Ok(new { id = applicationId, message = "Ứng tuyển thành công. Doanh nghiệp sẽ cập nhật trạng thái hồ sơ tại đây." });
        }
        catch (ApplicationWorkflowException exception)
        {
            return ToErrorResponse(exception);
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyApplications(
        [FromQuery] long? jobId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(AccountType.Candidate, out var userId, out var accessError)) return accessError!;

        try
        {
            return Ok(await _applicationService.GetCandidateApplicationsAsync(userId, jobId, cancellationToken));
        }
        catch (ApplicationWorkflowException exception)
        {
            return ToErrorResponse(exception);
        }
    }

    [HttpGet("employer")]
    public async Task<IActionResult> GetEmployerApplications(
        [FromQuery] long? jobId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(AccountType.Employer, out var userId, out var accessError)) return accessError!;

        try
        {
            return Ok(await _applicationService.GetEmployerApplicationsAsync(userId, jobId, cancellationToken));
        }
        catch (ApplicationWorkflowException exception)
        {
            return ToErrorResponse(exception);
        }
    }

    [HttpPut("{applicationId:long}/status")]
    public async Task<IActionResult> ChangeStatus(
        long applicationId,
        [FromBody] ChangeApplicationStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(AccountType.Employer, out var userId, out var accessError)) return accessError!;

        try
        {
            return Ok(await _applicationService.ChangeStatusAsync(userId, applicationId, request, cancellationToken));
        }
        catch (ApplicationWorkflowException exception)
        {
            return ToErrorResponse(exception);
        }
    }

    [HttpGet("{applicationId:long}/resume")]
    public async Task<IActionResult> DownloadApplicantResume(
        long applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(AccountType.Employer, out var userId, out var accessError)) return accessError!;

        try
        {
            var resume = await _applicationService.GetEmployerResumeAsync(userId, applicationId, cancellationToken);
            return File(resume.Content, resume.ContentType, resume.FileName);
        }
        catch (ApplicationWorkflowException exception)
        {
            return ToErrorResponse(exception);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "Không tìm thấy file CV đã nộp." });
        }
    }

    private bool TryGetUserId(AccountType expectedType, out Guid userId, out IActionResult? error)
    {
        userId = Guid.Empty;
        error = null;
        var accountType = User.FindFirst("account_type")?.Value ?? User.FindFirst("AccountType")?.Value;
        if (!string.Equals(accountType, expectedType.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            error = StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản không có quyền thực hiện thao tác này." });
            return false;
        }

        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("id")
            ?? User.FindFirstValue("UserId");
        if (!Guid.TryParse(id, out userId))
        {
            error = Unauthorized(new { message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." });
            return false;
        }

        return true;
    }

    private IActionResult ToErrorResponse(ApplicationWorkflowException exception) => exception.Error switch
    {
        ApplicationWorkflowError.CandidateProfileNotFound => NotFound(new { message = exception.Message }),
        ApplicationWorkflowError.ApplicationNotFound => NotFound(new { message = exception.Message }),
        ApplicationWorkflowError.EmployerAccessDenied => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
        ApplicationWorkflowError.AlreadyApplied => Conflict(new { message = exception.Message }),
        ApplicationWorkflowError.JobUnavailable => Conflict(new { message = exception.Message }),
        ApplicationWorkflowError.ResumeRequired => BadRequest(new { message = exception.Message }),
        ApplicationWorkflowError.CoverLetterTooLong => BadRequest(new { message = exception.Message }),
        ApplicationWorkflowError.InvalidStatus => BadRequest(new { message = exception.Message }),
        _ => BadRequest(new { message = "Yêu cầu chưa hợp lệ." })
    };
}
