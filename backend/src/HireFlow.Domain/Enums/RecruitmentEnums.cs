namespace HireFlow.Domain.Enums;

public enum JobStatus { Draft, Open, OnHold, Closed }
public enum ResumeProcessingStatus { Pending, Processing, Completed, Failed }
public enum BackgroundJobStatus { Queued, Processing, Completed, Failed, Cancelled }
public enum InterviewType { Technical, HR, Managerial }
public enum InterviewRecommendation { StrongNo, No, Neutral, Yes, StrongYes }
public enum CandidateSource { CareersPage, Referral, Agency, Sourced, Other }
