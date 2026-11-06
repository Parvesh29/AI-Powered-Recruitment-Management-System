using HireFlow.Domain.Common;

namespace HireFlow.Domain.Entities.Notifications;

public sealed class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string? DataJson { get; set; }
    public HireFlow.Domain.Entities.Organizations.User? User { get; set; }
}

public sealed class AuditLog : TenantEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? DataJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public HireFlow.Domain.Entities.Organizations.User? Actor { get; set; }
}

