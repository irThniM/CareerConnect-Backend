using CareerConnect.Application.Candidates.Requests;
using CareerConnect.Application.Candidates.Responses;
using CareerConnect.Application.Candidates.Services;
using CareerConnect.Application.Candidates.Models;
using CareerConnect.Application.Jobs.Responses;
using CareerConnect.Domain.Entities;
using CareerConnect.Domain.Enums;
using CareerConnect.Infrastructure.Ai;
using CareerConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareerConnect.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CandidateProfileController : ControllerBase
    {
        private const long MaxResumeBytes = 10 * 1024 * 1024;
        private static readonly JsonSerializerOptions ResumeJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly AppDbContext _context;
        private readonly IResumeStorageService _resumeStorage;
        private readonly IResumeProcessingService _resumeProcessing;
        private readonly IGeminiService _geminiService;
        private readonly ILogger<CandidateProfileController> _logger;

        public CandidateProfileController(
            AppDbContext context,
            IResumeStorageService resumeStorage,
            IResumeProcessingService resumeProcessing,
            IGeminiService geminiService,
            ILogger<CandidateProfileController> logger)
        {
            _context = context;
            _resumeStorage = resumeStorage;
            _resumeProcessing = resumeProcessing;
            _geminiService = geminiService;
            _logger = logger;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên. Vui lòng đăng nhập lại." });
            }

            if (profile.Resume is { ProcessingStatus: "PENDING" } pendingResume)
            {
                try
                {
                    var extension = Path.GetExtension(pendingResume.OriginalFileName).ToLowerInvariant();
                    var fileBytes = await _resumeStorage.DownloadAsync(
                        pendingResume.CloudinaryPublicId,
                        pendingResume.OriginalFileName,
                        extension.TrimStart('.'),
                        cancellationToken);
                    await using var stream = new MemoryStream(fileBytes, writable: false);
                    var processingResult = await _resumeProcessing.ProcessAsync(stream, extension, cancellationToken);
                    await ApplyResumeProcessingResultAsync(profile, pendingResume, processingResult, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(exception, "Failed to retrieve pending CV for candidate profile {CandidateProfileId}.", profile.Id);
                    pendingResume.ProcessingStatus = "FAILED";
                    pendingResume.ProcessingError = "Không tải được CV từ kho lưu trữ để phân tích. Hãy tải lại CV.";
                    pendingResume.ProcessedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return Ok(ToResponse(profile));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateCandidateProfileRequestDto request,
            CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên. Vui lòng đăng nhập lại." });
            }

            profile.FullName = request.FullName.Trim();
            profile.PhoneNumber = NormalizeOptional(request.PhoneNumber);
            profile.ProfessionalTitle = NormalizeOptional(request.ProfessionalTitle);
            profile.Address = NormalizeOptional(request.Address);
            profile.Summary = NormalizeOptional(request.Summary);
            profile.IsLookingForJob = request.IsLookingForJob;
            profile.UpdatedAt = DateTime.UtcNow;
            RefreshNormalizedProfileText(
                profile,
                profile.Resume?.ExtractedText,
                ReadStructuredData(profile.Resume?.StructuredData));
            profile.User.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(profile));
        }

        [HttpGet("me/recommendations")]
        public async Task<IActionResult> GetMyJobRecommendations(
            [FromQuery] int limit = 5,
            CancellationToken cancellationToken = default)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên." });
            }

            var embeddingText = BuildCandidateEmbeddingInput(profile);
            if (string.IsNullOrWhiteSpace(embeddingText))
            {
                return Ok(new CandidateJobRecommendationsResponseDto
                {
                    IsProfileReady = false,
                    Message = "Hãy thêm thông tin nghề nghiệp hoặc tải CV lên để nhận gợi ý việc làm."
                });
            }

            if (profile.Embedding is null || profile.EmbeddingUpdatedAt is null)
            {
                try
                {
                    var embeddingValues = await _geminiService.GetEmbeddingAsync(embeddingText, cancellationToken);
                    if (embeddingValues.Length != 3072)
                    {
                        _logger.LogError("Gemini returned {EmbeddingDimensions} dimensions for candidate profile {CandidateProfileId}; expected 3072.", embeddingValues.Length, profile.Id);
                        return StatusCode(StatusCodes.Status502BadGateway,
                            new { message = "Dịch vụ gợi ý trả về vector không đúng kích thước. Thử lại sau." });
                    }

                    profile.Embedding = new Vector(embeddingValues);
                    profile.EmbeddingUpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(exception, "Could not create candidate embedding for profile {CandidateProfileId}.", profile.Id);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new { message = "Tạm thời chưa tạo được gợi ý việc làm. Hồ sơ vẫn được lưu; bạn có thể thử lại sau." });
                }
            }

            var candidateEmbedding = profile.Embedding;
            var today = DateTime.UtcNow.Date;
            var region = GetCandidateRegion(profile.Address);
            var regionPattern = string.IsNullOrWhiteSpace(region) ? null : CreateIlikePattern(region);
            var jobsQuery = _context.JobProfiles
                .AsNoTracking()
                .Where(jobProfile => jobProfile.Embedding != null)
                .Where(jobProfile => jobProfile.Job.Status == "OPEN"
                    && jobProfile.Job.Company.Status == "ACTIVE"
                    && (!jobProfile.Job.ApplicationDeadline.HasValue || jobProfile.Job.ApplicationDeadline.Value >= today));

            if (regionPattern is not null)
            {
                jobsQuery = jobsQuery
                    .OrderByDescending(jobProfile => jobProfile.WorkMode == "REMOTE"
                        || EF.Functions.ILike(jobProfile.Location ?? string.Empty, regionPattern, "\\"))
                    .ThenByDescending(jobProfile => jobProfile.WorkMode == "REMOTE")
                    .ThenBy(jobProfile => jobProfile.Embedding!.CosineDistance(candidateEmbedding!));
            }
            else
            {
                jobsQuery = jobsQuery
                    .OrderByDescending(jobProfile => jobProfile.WorkMode == "REMOTE")
                    .ThenBy(jobProfile => jobProfile.Embedding!.CosineDistance(candidateEmbedding!));
            }

            var matches = await jobsQuery
                .Select(jobProfile => new
                {
                    Job = new JobListItemDto
                    {
                        Id = jobProfile.Job.Id,
                        Title = jobProfile.Job.Title,
                        Position = jobProfile.Job.Position,
                        CompanyName = jobProfile.Job.Company.CompanyName,
                        Industry = jobProfile.Job.Company.Industry,
                        CategoryName = jobProfile.Job.Category == null ? null : jobProfile.Job.Category.Name,
                        Location = jobProfile.Location,
                        SalaryMin = jobProfile.SalaryMin,
                        SalaryMax = jobProfile.SalaryMax,
                        IsSalaryNegotiable = jobProfile.IsSalaryNegotiable,
                        EmploymentType = jobProfile.EmploymentType,
                        WorkMode = jobProfile.WorkMode,
                        ExperienceRequired = jobProfile.ExperienceRequired,
                        Quantity = jobProfile.Quantity,
                        ApplicationDeadline = jobProfile.Job.ApplicationDeadline,
                        CreatedAt = jobProfile.Job.CreatedAt,
                        RequiredSkills = jobProfile.RequiredSkills
                            .Select(requiredSkill => requiredSkill.Skill.Name)
                            .OrderBy(name => name)
                            .ToList()
                    },
                    Distance = jobProfile.Embedding!.CosineDistance(candidateEmbedding!),
                    SameArea = regionPattern != null
                        && EF.Functions.ILike(jobProfile.Location ?? string.Empty, regionPattern, "\\"),
                    Remote = jobProfile.WorkMode == "REMOTE",
                    JobDescription = jobProfile.JobDescription,
                    JobRequirements = jobProfile.JobRequirements
                })
                .ToListAsync(cancellationToken);

            var candidateDomain = InferCareerDomain(profile.ProfessionalTitle)
                ?? InferCareerDomain(string.Join(" ", profile.Skills.Select(skill => skill.Skill.Name)))
                ?? InferCareerDomain(profile.Summary)
                ?? InferCareerDomain(profile.Resume?.ExtractedText);
            const int minimumSimilarityPercentage = 45;
            var relevantMatches = matches
                .Select(match => new
                {
                    match.Job,
                    SimilarityPercentage = (int)Math.Round(Math.Clamp((1d - match.Distance) * 100d, 0d, 100d)),
                    match.SameArea,
                    match.Remote,
                    JobDomain = InferCareerDomain(match.Job.Title + " " + match.Job.Position)
                        ?? InferCareerDomain(match.Job.CategoryName)
                        ?? InferCareerDomain(match.Job.Industry)
                        ?? InferCareerDomain(string.Join(" ", match.Job.RequiredSkills))
                        ?? InferCareerDomain(match.JobDescription + " " + match.JobRequirements)
                })
                .Where(match => match.SimilarityPercentage >= minimumSimilarityPercentage)
                .Where(match => candidateDomain is null || match.JobDomain is null || match.JobDomain == candidateDomain)
                .OrderBy(match => match.SameArea ? 0 : match.Remote ? 1 : 2)
                .ThenByDescending(match => match.SimilarityPercentage)
                .ToList();

            var selectedMatches = limit <= 0
                ? relevantMatches
                : relevantMatches.Take(Math.Clamp(limit, 1, 100)).ToList();

            var response = new CandidateJobRecommendationsResponseDto
            {
                IsProfileReady = true,
                TotalCount = relevantMatches.Count,
                Items = selectedMatches.Select(match => new CandidateJobRecommendationDto
                {
                    Job = match.Job,
                    SimilarityPercentage = match.SimilarityPercentage,
                    SameArea = match.SameArea,
                    Remote = match.Remote
                }).ToList(),
                Message = relevantMatches.Count == 0
                    ? "Chưa có tin tuyển dụng nào đạt mức độ liên quan phù hợp với hồ sơ của bạn."
                    : null
            };

            return Ok(response);
        }

        [HttpPost("me/cv")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxResumeBytes + 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxResumeBytes + 1024 * 1024)]
        public async Task<IActionResult> UploadMyResume(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file CV cần tải lên." });
            }
            if (file.Length > MaxResumeBytes)
            {
                return BadRequest(new { message = "File CV cần có dung lượng tối đa 10 MB." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = GetContentType(extension);
            if (contentType is null)
            {
                return BadRequest(new { message = "Chỉ hỗ trợ CV định dạng PDF hoặc DOCX." });
            }

            if (!await HasExpectedSignatureAsync(file, extension, cancellationToken))
            {
                return BadRequest(new { message = "Nội dung file không khớp với định dạng đã chọn." });
            }

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên. Vui lòng đăng nhập lại." });
            }

            var safeFileName = NormalizeFileName(file.FileName, extension);
            var previousPublicId = profile.Resume?.CloudinaryPublicId;
            StoredResumeAsset newAsset;
            byte[] fileBytes;

            await using (var input = file.OpenReadStream())
            using (var buffer = new MemoryStream())
            {
                await input.CopyToAsync(buffer, cancellationToken);
                fileBytes = buffer.ToArray();
            }

            ResumeProcessingResult processingResult;
            try
            {
                await using var stream = new MemoryStream(fileBytes, writable: false);
                processingResult = await _resumeProcessing.ProcessAsync(stream, extension, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "CV analysis failed before replacing candidate profile {CandidateProfileId}.", profile.Id);
                return UnprocessableEntity(new { message = GetResumeProcessingErrorMessage(exception) });
            }

            try
            {
                await using var stream = new MemoryStream(fileBytes, writable: false);
                newAsset = await _resumeStorage.UploadAsync(
                    profile.Id,
                    safeFileName,
                    extension.TrimStart('.'),
                    stream,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to upload candidate CV for profile {CandidateProfileId}.", profile.Id);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Không tải được CV lên kho lưu trữ. Vui lòng thử lại." });
            }

            var now = DateTime.UtcNow;
            var resume = profile.Resume ?? new Resume { CandidateProfileId = profile.Id };
            resume.SourceType = "UPLOADED";
            resume.FileUrl = newAsset.SecureUrl;
            resume.CloudinaryPublicId = newAsset.PublicId;
            resume.OriginalFileName = safeFileName;
            resume.ContentType = contentType;
            resume.FileSizeBytes = file.Length;
            resume.ExtractedText = null;
            resume.StructuredData = null;
            resume.ExtractionMethod = null;
            resume.ProcessingStatus = "PROCESSING";
            resume.ProcessingError = null;
            resume.ProcessedAt = null;
            resume.CreatedAt = now;
            resume.UpdatedAt = now;

            if (profile.Resume is null)
            {
                profile.Resume = resume;
                _context.Resumes.Add(resume);
            }
            profile.UpdatedAt = now;
            try
            {
                await ApplyResumeProcessingResultAsync(profile, resume, processingResult, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to save CV metadata for profile {CandidateProfileId}.", profile.Id);
                await TryDeleteAssetAsync(newAsset.PublicId, "new upload rollback");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "CV đã tải lên nhưng chưa lưu được thông tin. Vui lòng thử lại." });
            }

            if (!string.IsNullOrWhiteSpace(previousPublicId) && previousPublicId != newAsset.PublicId)
            {
                var isResumeAttachedToApplication = await _context.JobApplications
                    .AnyAsync(application => application.ResumeCloudinaryPublicIdSnapshot == previousPublicId, cancellationToken);
                if (!isResumeAttachedToApplication)
                {
                    await TryDeleteAssetAsync(previousPublicId, "replaced CV cleanup");
                }
            }

            return Ok(ToResponse(profile));
        }

        [HttpPost("me/cv/reprocess")]
        public async Task<IActionResult> ReprocessMyResume(CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            if (profile?.Resume is null)
            {
                return NotFound(new { message = "Hồ sơ của bạn chưa có CV để phân tích lại." });
            }

            byte[] fileBytes;
            var extension = Path.GetExtension(profile.Resume.OriginalFileName).ToLowerInvariant();
            try
            {
                fileBytes = await _resumeStorage.DownloadAsync(
                    profile.Resume.CloudinaryPublicId,
                    profile.Resume.OriginalFileName,
                    extension.TrimStart('.'),
                    cancellationToken);
            }
            catch (FileNotFoundException exception)
            {
                _logger.LogWarning(exception, "Candidate CV asset {PublicId} is missing during reprocessing.", profile.Resume.CloudinaryPublicId);
                return NotFound(new { message = "Không tìm thấy file CV trong kho lưu trữ. Hãy tải CV lên lại." });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to retrieve candidate CV {PublicId} for reprocessing.", profile.Resume.CloudinaryPublicId);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Chưa tải được CV từ kho lưu trữ để phân tích lại. Vui lòng thử lại." });
            }

            ResumeProcessingResult processingResult;
            try
            {
                await using var stream = new MemoryStream(fileBytes, writable: false);
                processingResult = await _resumeProcessing.ProcessAsync(stream, extension, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "CV reanalysis failed for candidate profile {CandidateProfileId}; keeping the previous parsed data.", profile.Id);
                return UnprocessableEntity(new { message = GetResumeProcessingErrorMessage(exception) });
            }

            await ApplyResumeProcessingResultAsync(profile, profile.Resume, processingResult, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(profile));
        }

        [HttpGet("me/cv/download")]
        public async Task<IActionResult> DownloadMyResume(CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var resume = await _context.Resumes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CandidateProfile.UserId == userId, cancellationToken);
            if (resume is null)
            {
                return NotFound(new { message = "Hồ sơ của bạn chưa có CV." });
            }

            try
            {
                var extension = Path.GetExtension(resume.OriginalFileName).TrimStart('.').ToLowerInvariant();
                var bytes = await _resumeStorage.DownloadAsync(
                    resume.CloudinaryPublicId,
                    resume.OriginalFileName,
                    extension,
                    cancellationToken);
                return File(bytes, resume.ContentType, resume.OriginalFileName);
            }
            catch (FileNotFoundException exception)
            {
                _logger.LogWarning(exception, "Candidate CV asset {PublicId} is missing.", resume.CloudinaryPublicId);
                return NotFound(new { message = "Không tìm thấy file CV trên kho lưu trữ." });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to download candidate CV {PublicId}.", resume.CloudinaryPublicId);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Chưa tải được CV. Vui lòng thử lại sau." });
            }
        }

        [HttpDelete("me/cv")]
        public async Task<IActionResult> DeleteMyResume(CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên." });
            }
            if (profile.Resume is null)
            {
                return NotFound(new { message = "Hồ sơ của bạn chưa có CV." });
            }

            var oldPublicId = profile.Resume.CloudinaryPublicId;
            var cvSuggestedSummary = ReadStructuredData(profile.Resume.StructuredData).SuggestedSummary;
            if (!string.IsNullOrWhiteSpace(profile.Summary)
                && !string.IsNullOrWhiteSpace(cvSuggestedSummary)
                && string.Equals(
                    Regex.Replace(profile.Summary, @"\s+", " ").Trim(),
                    Regex.Replace(cvSuggestedSummary, @"\s+", " ").Trim(),
                    StringComparison.Ordinal))
            {
                // Remove the summary only when it is still exactly the text that
                // this CV analysis inserted. A summary edited by the candidate stays.
                profile.Summary = null;
            }

            var cvSkills = profile.Skills.Where(skill => skill.SourceType == "CV").ToList();
            _context.CandidateSkills.RemoveRange(cvSkills);
            foreach (var skill in cvSkills) profile.Skills.Remove(skill);
            _context.Resumes.Remove(profile.Resume);
            profile.Resume = null;
            profile.UpdatedAt = DateTime.UtcNow;
            RefreshNormalizedProfileText(profile, resumeText: null);
            await _context.SaveChangesAsync(cancellationToken);
            var isResumeAttachedToApplication = await _context.JobApplications
                .AnyAsync(application => application.ResumeCloudinaryPublicIdSnapshot == oldPublicId, cancellationToken);
            if (!isResumeAttachedToApplication)
            {
                await TryDeleteAssetAsync(oldPublicId, "deleted CV cleanup");
            }

            return Ok(ToResponse(profile));
        }

        [HttpPut("me/cv/sections")]
        public async Task<IActionResult> UpdateMyResumeSections(
            [FromBody] UpdateCandidateResumeSectionsRequestDto request,
            CancellationToken cancellationToken)
        {
            var accessError = ValidateCandidate(out var userId);
            if (accessError is not null) return accessError;

            if (request.WorkExperiences is null || request.Projects is null || request.Education is null)
            {
                return BadRequest(new { message = "Vui lòng gửi đủ các danh sách kinh nghiệm, dự án và học vấn." });
            }

            var sectionGroups = new[] { request.WorkExperiences, request.Projects, request.Education };
            if (sectionGroups.Any(items => items.Count > 20 || items.Any(item =>
                    item is null
                    || string.IsNullOrWhiteSpace(item.Title)
                    || item.Title.Trim().Length > 180
                    || (item.Organization?.Trim().Length ?? 0) > 180
                    || (item.Period?.Trim().Length ?? 0) > 80
                    || (item.Description?.Trim().Length ?? 0) > 2400)))
            {
                return BadRequest(new { message = "Mỗi mục cần có tiêu đề hợp lệ; mỗi nhóm tối đa 20 mục." });
            }

            var profile = await _context.CandidateProfiles
                .Include(candidate => candidate.User)
                .Include(candidate => candidate.Resume)
                .Include(candidate => candidate.Skills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            if (profile is null)
            {
                return NotFound(new { message = "Chưa tìm thấy hồ sơ ứng viên." });
            }
            if (profile.Resume is null)
            {
                return BadRequest(new { message = "Hãy tải CV lên trước khi cập nhật các mục được trích xuất từ CV." });
            }

            var structuredData = ReadStructuredData(profile.Resume.StructuredData);
            structuredData.WorkExperiences = NormalizeSectionItems(request.WorkExperiences);
            structuredData.Projects = NormalizeSectionItems(request.Projects);
            structuredData.Education = NormalizeSectionItems(request.Education);
            profile.Resume.StructuredData = JsonSerializer.Serialize(structuredData, ResumeJsonOptions);
            profile.Resume.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
            RefreshNormalizedProfileText(profile, profile.Resume.ExtractedText, structuredData);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(profile));
        }

        private async Task TryDeleteAssetAsync(string publicId, string operation)
        {
            try
            {
                await _resumeStorage.DeleteAsync(publicId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cloudinary CV cleanup failed after {Operation} for asset {PublicId}.", operation, publicId);
            }
        }

        private IActionResult? ValidateCandidate(out Guid userId)
        {
            userId = Guid.Empty;

            if (User.FindFirst("account_type")?.Value != AccountType.Candidate.ToString())
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Chỉ tài khoản ứng viên mới được xem hoặc cập nhật hồ sơ này." });
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? User.FindFirstValue("sub")
                              ?? User.FindFirstValue("id")
                              ?? User.FindFirstValue("UserId");

            if (!Guid.TryParse(userIdValue, out userId))
            {
                return Unauthorized(new { message = "Không thể xác định tài khoản. Vui lòng đăng nhập lại." });
            }

            return null;
        }

        private static CandidateProfileResponseDto ToResponse(CandidateProfile profile)
        {
            var structuredData = ReadStructuredData(profile.Resume?.StructuredData);
            return new CandidateProfileResponseDto
            {
                Email = profile.User.Email,
                FullName = profile.FullName,
                PhoneNumber = profile.PhoneNumber,
                ProfessionalTitle = profile.ProfessionalTitle,
                Address = profile.Address,
                Summary = profile.Summary,
                IsLookingForJob = profile.IsLookingForJob,
                UpdatedAt = profile.UpdatedAt,
                Skills = profile.Skills
                    .Select(candidateSkill => candidateSkill.Skill.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList(),
                Resume = profile.Resume is null ? null : new CandidateResumeResponseDto
                {
                    FileName = profile.Resume.OriginalFileName,
                    FileSizeBytes = profile.Resume.FileSizeBytes,
                    ContentType = profile.Resume.ContentType,
                    ProcessingStatus = profile.Resume.ProcessingStatus,
                    ProcessingError = profile.Resume.ProcessingError,
                    UploadedAt = profile.Resume.CreatedAt,
                    DownloadUrl = "CandidateProfile/me/cv/download",
                    SuggestedFullName = structuredData.SuggestedFullName,
                    WorkExperiences = structuredData.WorkExperiences,
                    Projects = structuredData.Projects,
                    Education = structuredData.Education
                }
            };
        }

        private static CandidateResumeStructuredData ReadStructuredData(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new CandidateResumeStructuredData();
            try
            {
                return JsonSerializer.Deserialize<CandidateResumeStructuredData>(json, ResumeJsonOptions)
                       ?? new CandidateResumeStructuredData();
            }
            catch (JsonException)
            {
                return new CandidateResumeStructuredData();
            }
        }

        private static List<CandidateResumeSectionDto> NormalizeSectionItems(
            IEnumerable<CandidateResumeSectionDto> items) => items
            .Select(item => new CandidateResumeSectionDto
            {
                Title = item.Title.Trim(),
                Organization = NormalizeOptional(item.Organization),
                Period = NormalizeOptional(item.Period),
                Description = NormalizeOptional(item.Description)
            })
            .ToList();

        private async Task ApplyResumeProcessingResultAsync(
            CandidateProfile profile,
            Resume resume,
            ResumeProcessingResult result,
            CancellationToken cancellationToken)
        {
            resume.ProcessingStatus = "PROCESSING";
            resume.ProcessingError = null;
            var suggestedFullName = NormalizeSuggestedFullName(result.SuggestedFullName);
            if (!IsPlausibleSuggestedName(suggestedFullName))
            {
                suggestedFullName = ExtractNameFromResumeFileName(resume.OriginalFileName);
            }

            var names = result.Skills.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingSkills = await _context.Skills
                .Where(skill => names.Contains(skill.Name))
                .ToListAsync(cancellationToken);
            var skillsByName = existingSkills
                .GroupBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var previousCvSkills = profile.Skills.Where(skill => skill.SourceType == "CV").ToList();
            _context.CandidateSkills.RemoveRange(previousCvSkills);
            foreach (var previousSkill in previousCvSkills) profile.Skills.Remove(previousSkill);

            foreach (var definition in result.Skills)
            {
                if (!skillsByName.TryGetValue(definition.Name, out var skill))
                {
                    skill = new Skill { Name = definition.Name, Category = definition.Category, IsActive = true };
                    _context.Skills.Add(skill);
                    skillsByName[definition.Name] = skill;
                }

                var candidateSkill = new CandidateSkill
                {
                    CandidateProfile = profile,
                    Skill = skill,
                    SourceType = "CV"
                };
                profile.Skills.Add(candidateSkill);
                _context.CandidateSkills.Add(candidateSkill);
            }

            resume.ExtractedText = result.ExtractedText;
            resume.ExtractionMethod = result.ExtractionMethod;
            resume.StructuredData = JsonSerializer.Serialize(new CandidateResumeStructuredData
            {
                ExtractedSkills = result.Skills.Select(skill => new CandidateResumeSkillDto
                {
                    Name = skill.Name,
                    Category = skill.Category
                }).ToList(),
                SuggestedFullName = suggestedFullName,
                SuggestedSummary = result.SuggestedSummary,
                TextLength = result.ExtractedText.Length,
                WorkExperiences = result.WorkExperiences.Select(ToSectionDto).ToList(),
                Projects = result.Projects.Select(ToSectionDto).ToList(),
                Education = result.Education.Select(ToSectionDto).ToList()
            }, ResumeJsonOptions);
            resume.ProcessingStatus = "COMPLETED";
            resume.ProcessingError = null;
            resume.ProcessedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(profile.Summary) && !string.IsNullOrWhiteSpace(result.SuggestedSummary))
            {
                profile.Summary = result.SuggestedSummary;
            }

            if (!string.IsNullOrWhiteSpace(suggestedFullName))
            {
                // A successfully recognized name from the uploaded CV replaces
                // the existing profile name, even when the candidate already had one.
                profile.FullName = suggestedFullName;
                profile.User.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (string.IsNullOrWhiteSpace(profile.PhoneNumber) && !string.IsNullOrWhiteSpace(result.SuggestedPhoneNumber))
            {
                profile.PhoneNumber = result.SuggestedPhoneNumber;
            }

            var addressHasOcrIconNoise = !string.IsNullOrWhiteSpace(profile.Address)
                && Regex.IsMatch(profile.Address, @"^\s*['""“”‘’]?\s*\d{1,2}\s+(?=(?:phường|phuong|phương|ward)\b)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var addressIsIncomplete = !string.IsNullOrWhiteSpace(profile.Address)
                && Regex.IsMatch(profile.Address.Trim(), @"^(?:phường|phuong|phương|ward|quận|quan|huyện|huyen)\.?$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if ((string.IsNullOrWhiteSpace(profile.Address) || addressHasOcrIconNoise || addressIsIncomplete)
                && !string.IsNullOrWhiteSpace(result.SuggestedLocation))
            {
                profile.Address = result.SuggestedLocation;
            }

            resume.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
            RefreshNormalizedProfileText(profile, resume.ExtractedText, ReadStructuredData(resume.StructuredData));
        }

        private static string GetResumeProcessingErrorMessage(Exception exception) => exception switch
        {
            NotSupportedException => exception.Message,
            InvalidDataException => exception.Message,
            InvalidOperationException => "Không chạy được OCR. Hãy kiểm tra Tesseract và bộ tiếng Việt trên máy đang chạy backend.",
            _ => "Không phân tích được nội dung CV. Hãy thử file PDF hoặc DOCX có thể chọn và sao chép văn bản."
        };

        private static string BuildCandidateEmbeddingInput(CandidateProfile profile)
        {
            var structuredData = ReadStructuredData(profile.Resume?.StructuredData);
            var skills = string.Join(", ", profile.Skills
                .Select(candidateSkill => candidateSkill.Skill.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30));

            var sections = new[]
            {
                (Label: "Kinh nghiệm", Items: structuredData.WorkExperiences.Take(3)),
                (Label: "Dự án", Items: structuredData.Projects.Take(3)),
                (Label: "Học vấn", Items: structuredData.Education.Take(3))
            };
            var blocks = new List<string>();
            AddEmbeddingBlock(blocks, "Vị trí mong muốn", profile.ProfessionalTitle, 180);
            AddEmbeddingBlock(blocks, "Tóm tắt hồ sơ", profile.Summary, 700);
            AddEmbeddingBlock(blocks, "Kỹ năng", skills, 700);

            foreach (var sectionGroup in sections)
            {
                var items = string.Join("; ", sectionGroup.Items.Select(FormatSectionForEmbedding));
                if (!string.IsNullOrWhiteSpace(items)) blocks.Add($"{sectionGroup.Label}: {items}");
            }

            var resumeExtractedText = profile.Resume?.ExtractedText;
            var extractedText = string.IsNullOrWhiteSpace(resumeExtractedText)
                ? profile.NormalizedText
                : resumeExtractedText;
            AddEmbeddingBlock(blocks, "Nội dung CV", extractedText, 1200);

            // Avoid calling Gemini for an empty profile or one containing only a location.
            if (blocks.Count == 0) return string.Empty;

            var location = ClipEmbeddingField(profile.Address, 180);
            if (!string.IsNullOrWhiteSpace(location))
            {
                blocks.Insert(Math.Min(1, blocks.Count), $"Khu vực: {location}");
            }

            var text = Regex.Replace(string.Join("\n", blocks), @"\s+", " ").Trim();
            const int maximumCharacters = 4500;
            return text.Length <= maximumCharacters ? text : text[..maximumCharacters];
        }

        private static void AddEmbeddingBlock(List<string> blocks, string label, string? value, int maximumCharacters)
        {
            var clipped = ClipEmbeddingField(value, maximumCharacters);
            if (!string.IsNullOrWhiteSpace(clipped)) blocks.Add($"{label}: {clipped}");
        }

        private static string FormatSectionForEmbedding(CandidateResumeSectionDto section)
        {
            return string.Join(" ", new[]
            {
                ClipEmbeddingField(section.Title, 150),
                ClipEmbeddingField(section.Organization, 100),
                ClipEmbeddingField(section.Period, 80),
                ClipEmbeddingField(section.Description, 160)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string ClipEmbeddingField(string? value, int maximumCharacters)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = Regex.Replace(value, @"\s+", " ").Trim();
            return normalized.Length <= maximumCharacters ? normalized : normalized[..maximumCharacters];
        }

        private static string? GetCandidateRegion(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            var parts = address.Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts[^1];
        }

        private static string? InferCareerDomain(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = NormalizeCareerDomainText(value);
            var domains = new (string Name, string[] Terms)[]
            {
                ("accounting-finance", ["ke toan", "accounting", "accountant", "kiem toan", "auditor", "audit", "tai chinh", "finance", "financial", "payroll", "thue", "tax", "ngan hang"]),
                ("information-technology", ["cong nghe thong tin", "lap trinh", "programming", "programmer", "developer", "software", "backend", "frontend", "full stack", "devops", "quality assurance", "tester", "kiem thu", "data engineer", "data scientist", "machine learning", "artificial intelligence", ".net", "c#", "api", "qa", "it"]),
                ("sales", ["ban hang", "sales", "kinh doanh", "business development", "account executive", "tu van ban hang"]),
                ("marketing-communications", ["marketing", "seo", "content marketing", "truyen thong", "quang cao", "public relations", "branding"]),
                ("human-resources", ["nhan su", "human resources", "recruiter", "recruitment", "tuyen dung", "hr"]),
                ("design", ["graphic design", "ui ux", "ui/ux", "product design", "thiet ke do hoa", "thiet ke giao dien"]),
                ("healthcare", ["y te", "healthcare", "nurse", "doctor", "duoc si", "bac si", "dieu duong"]),
                ("education", ["giao duc", "education", "teacher", "lecturer", "giao vien", "giang vien"]),
                ("construction", ["xay dung", "construction", "kien truc", "architect", "civil engineering", "co khi"]),
                ("legal", ["phap ly", "legal", "lawyer", "luat su", "compliance"]),
                ("logistics", ["logistics", "supply chain", "chuoi cung ung", "van tai", "warehouse", "kho van"]),
                ("customer-service", ["cham soc khach hang", "customer service", "customer support", "hotline"]),
                ("administration", ["hanh chinh", "administrative", "office assistant", "van thu"]),
                ("hospitality", ["du lich", "tourism", "hospitality", "hotel", "nha hang", "khach san"])
            };

            foreach (var domain in domains)
            {
                if (domain.Terms.Any(term => MatchesCareerTerm(normalized, term))) return domain.Name;
            }

            return null;
        }

        private static string NormalizeCareerDomainText(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return Regex.Replace(builder.ToString()
                    .Replace('đ', 'd')
                    .Replace('Đ', 'D')
                    .ToLowerInvariant(),
                @"[^\p{L}\p{N}+#.]+", " ").Trim();
        }

        private static bool MatchesCareerTerm(string text, string term)
        {
            var normalizedTerm = NormalizeCareerDomainText(term);
            if (normalizedTerm.Contains(' ')) return text.Contains(normalizedTerm, StringComparison.Ordinal);
            return Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(normalizedTerm)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.CultureInvariant);
        }

        private static string CreateIlikePattern(string value) =>
            $"%{value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

        private static string BuildNormalizedProfileText(
            CandidateProfile profile,
            string? resumeText,
            CandidateResumeStructuredData? structuredData = null)
        {
            var structuredText = structuredData is null
                ? null
                : string.Join(" ", structuredData.WorkExperiences
                    .Concat(structuredData.Projects)
                    .Concat(structuredData.Education)
                    .SelectMany(section => new[] { section.Title, section.Organization, section.Period, section.Description })
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            var skillText = string.Join(" ", profile.Skills
                .Select(candidateSkill => candidateSkill.Skill.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase));

            return string.Join(" ", new[]
            {
                profile.ProfessionalTitle,
                profile.Address,
                profile.Summary,
                skillText,
                resumeText,
                structuredText
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static void RefreshNormalizedProfileText(
            CandidateProfile profile,
            string? resumeText,
            CandidateResumeStructuredData? structuredData = null)
        {
            var normalizedText = BuildNormalizedProfileText(profile, resumeText, structuredData);
            if (string.Equals(profile.NormalizedText, normalizedText, StringComparison.Ordinal)) return;

            profile.NormalizedText = normalizedText;
            profile.Embedding = null;
            profile.EmbeddingUpdatedAt = null;
        }

        private static CandidateResumeSectionDto ToSectionDto(ResumeSectionItem item) => new()
        {
            Title = item.Title,
            Organization = item.Organization,
            Period = item.Period,
            Description = item.Description
        };

        private static string? GetContentType(string extension) => extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => null
        };

        private static string NormalizeFileName(string fileName, string extension)
        {
            var safeName = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
            if (string.IsNullOrWhiteSpace(safeName)) safeName = $"CV{extension}";
            if (safeName.Length <= 255) return safeName;

            var stem = Path.GetFileNameWithoutExtension(safeName);
            var maxStemLength = 255 - extension.Length;
            return $"{stem[..Math.Min(stem.Length, maxStemLength)]}{extension}";
        }

        private static async Task<bool> HasExpectedSignatureAsync(
            IFormFile file,
            string extension,
            CancellationToken cancellationToken)
        {
            var header = new byte[8];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);

            return extension switch
            {
                ".pdf" => read >= 5 && Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",
                ".docx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
                _ => false
            };
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeSuggestedFullName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var normalized = Regex.Replace(value, @"\s+", " ").Trim();
            var vietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
            return vietnameseCulture.TextInfo.ToTitleCase(normalized.ToLower(vietnameseCulture));
        }

        private static bool IsPlausibleSuggestedName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is < 2 or > 5 || value.Any(char.IsDigit)) return false;

            var normalized = Regex.Replace(value.Normalize(NormalizationForm.FormD), @"\p{Mn}", string.Empty)
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
            string[] nonNamePhrases =
            [
                "hien tai", "hien nay", "currently", "present", "now", "today", "dang lam",
                "hoan thanh", "tot nghiep", "ho chi minh", "ha noi", "da nang", "phuong", "quan", "huyen",
                "backend", "frontend", "developer", "engineer", "designer", "student", "intern", "resume", "candidate"
            ];
            return !nonNamePhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
        }

        private static string? ExtractNameFromResumeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            var stem = Path.GetFileNameWithoutExtension(fileName)
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();
            stem = Regex.Replace(stem, @"^(?:cv|resume|ho so|so yeu ly lich)\s*", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var candidate = NormalizeSuggestedFullName(stem);
            return IsPlausibleSuggestedName(candidate) ? candidate : null;
        }
    }
}
