namespace CareerConnect.Application.Candidates.Services
{
    public sealed record StoredResumeAsset(string SecureUrl, string PublicId);

    public interface IResumeStorageService
    {
        Task<StoredResumeAsset> UploadAsync(
            long candidateProfileId,
            string fileName,
            string extension,
            Stream content,
            CancellationToken cancellationToken = default);

        Task<byte[]> DownloadAsync(
            string publicId,
            string fileName,
            string extension,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
    }
}
