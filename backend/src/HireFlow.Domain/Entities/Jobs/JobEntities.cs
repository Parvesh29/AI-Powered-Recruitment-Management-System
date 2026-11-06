using HireFlow.Domain.Common;
using HireFlow.Domain.Enums;

namespace HireFlow.Domain.Entities.Jobs;

public sealed class Job : TenantEntity
{
    public Guid DepartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? EmploymentType { get; set; }
    public int MinimumExperienceYears { get; set; }
    public int? MaximumExperienceYears { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public HireFlow.Domain.Entities.Organizations.Department? Department { get; set; }
}

public sealed class Skill : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
}

public sealed class JobSkill : TenantEntity
{
    public Guid JobId { get; set; }
    public Guid SkillId { get; set; }
    public bool IsRequired { get; set; }
    public Job? Job { get; set; }
    public Skill? Skill { get; set; }
}

public sealed class JobDepartment : TenantEntity
{
    public Guid JobId { get; set; }
    public Guid DepartmentId { get; set; }
    public Job? Job { get; set; }
    public HireFlow.Domain.Entities.Organizations.Department? Department { get; set; }
}

public sealed class JobRecruiter : TenantEntity
{
    public Guid JobId { get; set; }
    public Guid RecruiterUserId { get; set; }
    public Job? Job { get; set; }
    public HireFlow.Domain.Entities.Organizations.User? Recruiter { get; set; }
}


