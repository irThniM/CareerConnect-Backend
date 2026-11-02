using CareerConnect.Application.Employer.Requests;
using CareerConnect.Application.Employer.Exceptions;
using CareerConnect.Application.Jobs.Exceptions;
using CareerConnect.Application.Jobs.Services;
using CareerConnect.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CareerConnect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployerJobController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly ILogger<EmployerJobController> _logger;

        public EmployerJobController(IJobService jobService, ILogger<EmployerJobController> logger)
        {
            _jobService = jobService;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var accountType = User.FindFirst("account_type")?.Value ?? User.FindFirst("AccountType")?.Value;
            if (accountType != AccountType.Employer.ToString())
            {
                return StatusCode(403, new { message = "Chỉ tài khoản Nhà tuyển dụng mới được xem bảng điều khiển này." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value
                              ?? User.FindFirst("id")?.Value
                              ?? User.FindFirst("UserId")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Không thể xác định tài khoản. Vui lòng đăng nhập lại." });
            }

            var dashboard = await _jobService.GetEmployerDashboardAsync(userId);
            return dashboard is null
                ? NotFound(new { message = "Bạn chưa có hồ sơ doanh nghiệp." })
                : Ok(dashboard);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobRequestDto request)
        {
            var accountType = User.FindFirst("account_type")?.Value ?? User.FindFirst("AccountType")?.Value;
            if (accountType != AccountType.Employer.ToString())
            {
                return StatusCode(403, new { message = "Chỉ tài khoản Doanh nghiệp mới được đăng tin." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Không thể xác định tài khoản. Vui lòng đăng nhập lại." });
            }

            try
            {
                var resultMessage = await _jobService.CreateJobAsync(userId, request);
                return Ok(new { message = resultMessage });
            }
            catch (InvalidJobCategoryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (JobPostCreditsExhaustedException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (JobPostingNotAllowedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to create a job for user {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể đăng tin lúc này. Vui lòng thử lại sau." });
            }
        }

        [HttpGet("{jobId:long}/edit")]
        public async Task<IActionResult> GetJobForEdit(long jobId, CancellationToken cancellationToken)
        {
            if (!TryGetEmployerUserId(out var userId, out var accessError)) return accessError!;

            try
            {
                var job = await _jobService.GetEmployerJobForEditAsync(userId, jobId, cancellationToken);
                return job is null ? NotFound(new { message = "Không tìm thấy tin tuyển dụng." }) : Ok(job);
            }
            catch (JobPostingNotAllowedException exception)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
            }
        }

        [HttpPut("{jobId:long}")]
        public async Task<IActionResult> UpdateJob(
            long jobId,
            [FromBody] CreateJobRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetEmployerUserId(out var userId, out var accessError)) return accessError!;

            try
            {
                var updated = await _jobService.UpdateJobAsync(userId, jobId, request, cancellationToken);
                return updated ? Ok(new { message = "Đã cập nhật tin tuyển dụng." }) : NotFound(new { message = "Không tìm thấy tin tuyển dụng." });
            }
            catch (InvalidJobCategoryException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (InvalidJobSkillException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (JobPostingNotAllowedException exception)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to update job {JobId} for user {UserId}.", jobId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể cập nhật tin lúc này. Vui lòng thử lại sau." });
            }
        }

        [HttpPut("{jobId:long}/deadline")]
        public async Task<IActionResult> ExtendJobDeadline(
            long jobId,
            [FromBody] ExtendJobDeadlineRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetEmployerUserId(out var userId, out var accessError)) return accessError!;

            try
            {
                var updated = await _jobService.ExtendJobDeadlineAsync(userId, jobId, request.ApplicationDeadline, cancellationToken);
                return updated ? Ok(new { message = "Đã gia hạn hạn nhận hồ sơ." }) : NotFound(new { message = "Không tìm thấy tin tuyển dụng." });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (JobPostingNotAllowedException exception)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to extend job {JobId} deadline for user {UserId}.", jobId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể gia hạn tin lúc này. Vui lòng thử lại sau." });
            }
        }

        private bool TryGetEmployerUserId(out Guid userId, out IActionResult? accessError)
        {
            var accountType = User.FindFirst("account_type")?.Value ?? User.FindFirst("AccountType")?.Value;
            if (accountType != AccountType.Employer.ToString())
            {
                userId = Guid.Empty;
                accessError = StatusCode(403, new { message = "Chỉ tài khoản Nhà tuyển dụng mới được quản lý tin." });
                return false;
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value
                              ?? User.FindFirst("id")?.Value
                              ?? User.FindFirst("UserId")?.Value;
            if (!Guid.TryParse(userIdClaim, out userId))
            {
                accessError = Unauthorized(new { message = "Không thể xác định tài khoản. Vui lòng đăng nhập lại." });
                return false;
            }

            accessError = null;
            return true;
        }
    }
}
