using System.ComponentModel.DataAnnotations.Schema;

namespace HireFlow.Domain.Common;

[NotMapped]
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

[NotMapped]
public abstract class AuditableEntity : EntityBase
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
}

[NotMapped]
public abstract class TenantEntity : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public HireFlow.Domain.Entities.Organizations.Organization? Organization { get; set; }
}


