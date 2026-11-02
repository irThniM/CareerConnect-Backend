using CareerConnect.Application.Messaging.Requests;
using CareerConnect.Application.Messaging.Responses;

namespace CareerConnect.Application.Messaging.Services;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationSummaryDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid userId, long conversationId, CancellationToken cancellationToken = default);
    Task<long> SendMessageAsync(Guid userId, long conversationId, SendMessageRequestDto request, CancellationToken cancellationToken = default);
}
