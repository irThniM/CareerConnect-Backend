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
    }
}
