using CareerConnect.Application.Feed.Requests;
using CareerConnect.Application.Feed.Responses;

namespace CareerConnect.Application.Feed.Services;

public interface IFeedService
{
    Task<IReadOnlyList<FeedPostDto>> GetPublishedPostsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<long> CreatePostAsync(Guid authorUserId, CreatePostRequestDto request, CancellationToken cancellationToken = default);
    Task<long> AddCommentAsync(Guid userId, long postId, CreateCommentRequestDto request, CancellationToken cancellationToken = default);
    Task ModeratePostAsync(Guid adminUserId, long postId, string status, CancellationToken cancellationToken = default);
}
