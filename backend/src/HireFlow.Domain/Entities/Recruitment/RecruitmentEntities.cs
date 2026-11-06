using HireFlow.Domain.Common;
using HireFlow.Domain.Enums;

namespace HireFlow.Domain.Entities.Recruitment;

public sealed class RecruitmentStage : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Application : TenantEntity
{
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public Guid CurrentStageId { get; set; }
    public CandidateSource Source { get; set; } = CandidateSource.Other;
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public RecruitmentStage? CurrentStage { get; set; }
    public Candidates.Candidate? Candidate { get; set; }
    public Jobs.Job? Job { get; set; }
}

public sealed class ApplicationStageHistory : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public Guid? FromStageId { get; set; }
    public Guid ToStageId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
    public Application? Application { get; set; }
    public RecruitmentStage? FromStage { get; set; }
    public RecruitmentStage? ToStage { get; set; }
}

public sealed class Interview : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public InterviewType Type { get; set; }
    public int RoundNumber { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? MeetingUrl { get; set; }
    public string Status { get; set; } = "Scheduled";
    public Application? Application { get; set; }
}

public sealed class InterviewParticipant : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Guid UserId { get; set; }
    public Interview? Interview { get; set; }
    public HireFlow.Domain.Entities.Organizations.User? User { get; set; }
}

public sealed class InterviewFeedback : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public int Rating { get; set; }
    public InterviewRecommendation Recommendation { get; set; }
    public string? Strengths { get; set; }
    public string? Concerns { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public Interview? Interview { get; set; }
    public HireFlow.Domain.Entities.Organizations.User? SubmittedBy { get; set; }
}

