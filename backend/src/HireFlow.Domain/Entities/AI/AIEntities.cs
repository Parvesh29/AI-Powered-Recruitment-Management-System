using HireFlow.Domain.Common;
using HireFlow.Domain.Enums;

namespace HireFlow.Domain.Entities.AI;

public sealed class ResumeParsingResult : TenantEntity
{
    public Guid ResumeId { get; set; }
    public ResumeProcessingStatus Status { get; set; }
    public string ParserVersion { get; set; } = string.Empty;
    public string ParsedDataJson { get; set; } = "{}";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Candidates.Resume? Resume { get; set; }
}

public sealed class CandidateMatchScore : TenantEntity
{
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public decimal Score { get; set; }
    public string MatchedSkillsJson { get; set; } = "[]";
    public string MissingSkillsJson { get; set; } = "[]";
    public string ExplanationJson { get; set; } = "{}";
    public string AlgorithmVersion { get; set; } = string.Empty;
    public Candidates.Candidate? Candidate { get; set; }
    public Jobs.Job? Job { get; set; }
}

public sealed class AIProcessingJob : TenantEntity
{
    public string JobType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Queued;
    public string? PayloadJson { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AIUsageLog : TenantEntity
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
