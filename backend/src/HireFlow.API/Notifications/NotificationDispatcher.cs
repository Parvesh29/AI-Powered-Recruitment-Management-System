using System.Text.Json;
using HireFlow.Domain.Entities.Notifications;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace HireFlow.API.Notifications;

public static class NotificationDispatcher
{
    public static async Task SendAsync(HireFlowDbContext db, IHubContext<NotificationsHub> hub, Guid organizationId, Guid userId,
        string type, string title, string message, object? data = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            OrganizationId = organizationId, UserId = userId, CreatedBy = userId,
            Type = type, Title = title, Message = message,
            DataJson = data is null ? null : JsonSerializer.Serialize(data)
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await hub.Clients.Group(NotificationsHub.GroupName(organizationId, userId)).SendAsync("notification", new
            {
                notification.Id, notification.Type, notification.Title, notification.Message, notification.DataJson, notification.CreatedAt
            }, cancellationToken);
        }
        catch (Exception) { /* Persisted notification history remains available if a live connection drops. */ }
    }
}
