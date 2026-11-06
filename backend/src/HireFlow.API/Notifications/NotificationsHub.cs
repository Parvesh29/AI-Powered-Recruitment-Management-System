using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HireFlow.API.Notifications;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var organizationId = Context.User?.FindFirstValue("org");
        var userId = Context.User?.FindFirstValue("sub");
        if (Guid.TryParse(organizationId, out var organization) && Guid.TryParse(userId, out var user))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organization, user));
        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid organizationId, Guid userId) => $"org:{organizationId:N}:user:{userId:N}";
}
