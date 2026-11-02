using CareerConnect.Application.Notifications.Responses;

namespace CareerConnect.Application.Notifications.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid userId, long notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
