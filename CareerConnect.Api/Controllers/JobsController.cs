using CareerConnect.Application.Jobs.Requests;
using CareerConnect.Application.Jobs.Responses;
using CareerConnect.Application.Jobs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareerConnect.Api.Controllers
{
    [Route("api/jobs")]
    [ApiController]
    [AllowAnonymous]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobsController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet("categories")]
        [ProducesResponseType(typeof(IReadOnlyList<JobCategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _jobService.GetActiveJobCategoriesAsync();
            return Ok(categories);
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] JobSearchRequestDto request)
        {
            var result = await _jobService.SearchPublicJobsAsync(request);
            return Ok(result);
        }

        [HttpGet("{jobId:long}")]
        public async Task<IActionResult> GetDetails(long jobId)
        {
            var job = await _jobService.GetPublicJobDetailsAsync(jobId);
            return job is null ? NotFound(new { message = "Không tìm thấy tin tuyển dụng." }) : Ok(job);
        }
    }
}
