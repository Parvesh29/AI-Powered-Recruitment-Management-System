using HireFlow.Domain.Common;
using HireFlow.Domain.Enums;

namespace HireFlow.Domain.Entities.Candidates;

public sealed class Candidate : TenantEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? ProfessionalSummary { get; set; }
    public decimal? TotalExperienceYears { get; set; }
    public Guid? UserId { get; set; }
    public HireFlow.Domain.Entities.Organizations.User? User { get; set; }
}

public sealed class Resume : TenantEntity
{
    public Guid CandidateId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public ResumeProcessingStatus ProcessingStatus { get; set; }
    public Candidate? Candidate { get; set; }
}

public sealed class CandidateSkill : TenantEntity
{
    public Guid CandidateId { get; set; }
    public Guid SkillId { get; set; }
    public decimal? YearsExperience { get; set; }
    public Candidate? Candidate { get; set; }
    public HireFlow.Domain.Entities.Jobs.Skill? Skill { get; set; }
}

public sealed class CandidateEducation : TenantEntity
{
    public Guid CandidateId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public Candidate? Candidate { get; set; }
}

public sealed class CandidateExperience : TenantEntity
{
    public Guid CandidateId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Candidate? Candidate { get; set; }
}


