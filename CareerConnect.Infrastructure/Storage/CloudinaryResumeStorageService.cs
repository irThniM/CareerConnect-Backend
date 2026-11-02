using CareerConnect.Application.Candidates.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace CareerConnect.Infrastructure.Storage
{
    public sealed class CloudinaryResumeStorageService : IResumeStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public CloudinaryResumeStorageService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<StoredResumeAsset> UploadAsync(
            long candidateProfileId,
            string fileName,
            string extension,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            var cloudinary = CreateCloudinary();
            var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
            var publicId = $"candidate-cvs/{candidateProfileId}/{Guid.NewGuid():N}.{normalizedExtension}";
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, content),
                PublicId = publicId,
                AssetFolder = "CareerConnect/CandidateCVs",
                DisplayName = Path.GetFileNameWithoutExtension(fileName),
                Overwrite = false,
                Type = "authenticated"
            };

            var result = await cloudinary.UploadAsync(uploadParams, "raw", cancellationToken);
            if (result.Error is not null)
            {
                throw new InvalidOperationException($"Cloudinary không nhận được file CV: {result.Error.Message}");
            }
            if (string.IsNullOrWhiteSpace(result.PublicId) || string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()))
            {
                throw new InvalidOperationException("Cloudinary không trả về thông tin file CV sau khi tải lên.");
            }

            return new StoredResumeAsset(result.SecureUrl.ToString(), result.PublicId);
        }

        public async Task<byte[]> DownloadAsync(
            string publicId,
            string fileName,
            string extension,
            CancellationToken cancellationToken = default)
        {
            var cloudinary = CreateCloudinary();
            var expiry = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds();
            var downloadUrl = cloudinary.DownloadPrivate(
                publicId,
                attachment: true,
                format: extension,
                type: "authenticated",
                expiresAt: expiry,
                resourceType: "raw",
                targetFilename: fileName);

            using var response = await _httpClientFactory.CreateClient().GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException("Không tìm thấy file CV trên kho lưu trữ.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            var result = await CreateCloudinary().DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Raw,
                Type = "authenticated",
                Invalidate = true
            });

            if (result.Error is not null || (result.Result != "ok" && result.Result != "not found"))
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Cloudinary chưa xóa được CV cũ.");
            }
        }

        private Cloudinary CreateCloudinary()
        {
            var cloudName = _configuration["CloudinarySettings:CloudName"];
            var apiKey = _configuration["CloudinarySettings:ApiKey"];
            var apiSecret = _configuration["CloudinarySettings:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException("Cloudinary chưa được cấu hình cho chức năng tải CV.");
            }

            return new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        }
    }
}
