using HireFlow.Domain.Common;

namespace HireFlow.Domain.Entities.Organizations;

public sealed class PasswordResetToken : TenantEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public User? User { get; set; }
}
