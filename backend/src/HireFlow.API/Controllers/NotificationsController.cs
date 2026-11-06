using HireFlow.Application.Abstractions;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(HireFlowDbContext db, ICurrentTenant tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("Page must be positive and page size between 1 and 100.");
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == tenant.UserId).OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.Type, x.Title, x.Message, x.IsRead, x.CreatedAt, x.DataJson }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize, unreadCount = await db.Notifications.CountAsync(x => x.UserId == tenant.UserId && !x.IsRead, ct) });
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == tenant.UserId, ct);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
