using HireFlow.Domain.Common;
using HireFlow.Application.Abstractions;
using System.Linq.Expressions;
using HireFlow.Domain.Entities.AI;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Jobs;
using HireFlow.Domain.Entities.Notifications;
using HireFlow.Domain.Entities.Organizations;
using HireFlow.Domain.Entities.Recruitment;
using RecruitmentApplication = HireFlow.Domain.Entities.Recruitment.Application;
using HireFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.Persistence;

public sealed class HireFlowDbContext(DbContextOptions<HireFlowDbContext> options, ICurrentTenant currentTenant) : DbContext(options)
{
    private Guid CurrentOrganizationId => currentTenant.OrganizationId;
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<JobDepartment> JobDepartments => Set<JobDepartment>();
    public DbSet<JobRecruiter> JobRecruiters => Set<JobRecruiter>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();
    public DbSet<CandidateEducation> CandidateEducation => Set<CandidateEducation>();
    public DbSet<CandidateExperience> CandidateExperience => Set<CandidateExperience>();
    public DbSet<RecruitmentApplication> Applications => Set<RecruitmentApplication>();
    public DbSet<RecruitmentStage> RecruitmentStages => Set<RecruitmentStage>();
    public DbSet<ApplicationStageHistory> ApplicationStageHistory => Set<ApplicationStageHistory>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewParticipant> InterviewParticipants => Set<InterviewParticipant>();
    public DbSet<InterviewFeedback> InterviewFeedback => Set<InterviewFeedback>();
    public DbSet<ResumeParsingResult> ResumeParsingResults => Set<ResumeParsingResult>();
    public DbSet<CandidateMatchScore> CandidateMatchScores => Set<CandidateMatchScore>();
    public DbSet<AIProcessingJob> AIProcessingJobs => Set<AIProcessingJob>();
    public DbSet<AIUsageLog> AIUsageLogs => Set<AIUsageLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("hireflow");

        modelBuilder.Entity<Organization>(b =>
        {
            b.ToTable("organizations");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy);
        });

        ConfigureTenantEntities(modelBuilder);
        ConfigureOrganizationRelationships(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
        ConfigureIndexesAndConstraints(modelBuilder);
        ConfigureDomainRelationships(modelBuilder);
    }

    private void ConfigureTenantEntities(ModelBuilder modelBuilder)
    {
        var names = new Dictionary<Type, string>
        {
            [typeof(User)] = "users", [typeof(Role)] = "roles", [typeof(UserRole)] = "user_roles",
            [typeof(RolePermission)] = "role_permissions", [typeof(Department)] = "departments",
            [typeof(RefreshToken)] = "refresh_tokens", [typeof(PasswordResetToken)] = "password_reset_tokens", [typeof(Job)] = "jobs", [typeof(JobSkill)] = "job_skills", [typeof(JobDepartment)] = "job_departments",
            [typeof(JobRecruiter)] = "job_recruiters", [typeof(Candidate)] = "candidates", [typeof(Resume)] = "resumes",
            [typeof(CandidateSkill)] = "candidate_skills", [typeof(CandidateEducation)] = "candidate_education",
            [typeof(CandidateExperience)] = "candidate_experience", [typeof(RecruitmentApplication)] = "applications",
            [typeof(RecruitmentStage)] = "recruitment_stages", [typeof(ApplicationStageHistory)] = "application_stage_history",
            [typeof(Interview)] = "interviews", [typeof(InterviewParticipant)] = "interview_participants",
            [typeof(InterviewFeedback)] = "interview_feedback", [typeof(ResumeParsingResult)] = "resume_parsing_results",
            [typeof(CandidateMatchScore)] = "candidate_match_scores", [typeof(AIProcessingJob)] = "ai_processing_jobs",
            [typeof(AIUsageLog)] = "ai_usage_logs", [typeof(Notification)] = "notifications", [typeof(AuditLog)] = "audit_logs"
        };

        foreach (var (type, table) in names)
        {
            var entity = modelBuilder.Entity(type);
            entity.ToTable(table);
            entity.HasKey(nameof(EntityBase.Id));
            entity.HasAlternateKey(nameof(TenantEntity.OrganizationId), nameof(EntityBase.Id));
            entity.HasIndex(nameof(TenantEntity.OrganizationId));
            entity.Property(nameof(AuditableEntity.CreatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(nameof(AuditableEntity.UpdatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(nameof(AuditableEntity.CreatedBy));
            entity.HasOne(typeof(Organization), nameof(TenantEntity.Organization))
                .WithMany()
                .HasForeignKey(nameof(TenantEntity.OrganizationId))
                .OnDelete(DeleteBehavior.Cascade);
        }

        modelBuilder.Entity<User>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(320).IsRequired();
            b.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<Role>(b => { b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.Description).HasMaxLength(500); });
        modelBuilder.Entity<Department>(b => b.Property(x => x.Name).HasMaxLength(160).IsRequired());
        modelBuilder.Entity<RefreshToken>(b => { b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128); b.Property(x => x.RevokedAt).IsConcurrencyToken(); });
        modelBuilder.Entity<PasswordResetToken>(b => { b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); b.Property(x => x.UsedAt).IsConcurrencyToken(); });
        modelBuilder.Entity<Job>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).IsRequired();
            b.Property(x => x.Location).HasMaxLength(200);
            b.Property(x => x.EmploymentType).HasMaxLength(80);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<Candidate>(b =>
        {
            b.Property(x => x.FirstName).HasMaxLength(100);
            b.Property(x => x.LastName).HasMaxLength(100);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.NormalizedEmail).HasMaxLength(320);
            b.Property(x => x.PhoneNumber).HasMaxLength(40);
            b.Property(x => x.Location).HasMaxLength(200);
            b.Property(x => x.TotalExperienceYears).HasPrecision(5, 2);
        });
        modelBuilder.Entity<Resume>(b =>
        {
            b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            b.Property(x => x.Sha256Hash).HasMaxLength(64).IsRequired();
            b.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<CandidateSkill>(b => b.Property(x => x.YearsExperience).HasPrecision(5, 2));
        modelBuilder.Entity<RecruitmentStage>(b => b.Property(x => x.Name).HasMaxLength(80).IsRequired());
        modelBuilder.Entity<RecruitmentApplication>(b => b.Property(x => x.Source).HasConversion<string>().HasMaxLength(30));
        modelBuilder.Entity<Interview>(b =>
        {
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Status).HasMaxLength(30).IsRequired();
            b.Property(x => x.MeetingUrl).HasMaxLength(1000);
        });
        modelBuilder.Entity<InterviewFeedback>(b => b.Property(x => x.Recommendation).HasConversion<string>().HasMaxLength(20));
        modelBuilder.Entity<ResumeParsingResult>(b =>
        {
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.ParserVersion).HasMaxLength(80).IsRequired();
            b.Property(x => x.ParsedDataJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.ErrorCode).HasMaxLength(100);
        });
        modelBuilder.Entity<CandidateMatchScore>(b =>
        {
            b.Property(x => x.Score).HasPrecision(5, 2);
            b.Property(x => x.MatchedSkillsJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.MissingSkillsJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.ExplanationJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.AlgorithmVersion).HasMaxLength(80).IsRequired();
        });
        modelBuilder.Entity<AIProcessingJob>(b =>
        {
            b.Property(x => x.JobType).HasMaxLength(100).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<AIUsageLog>(b =>
        {
            b.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            b.Property(x => x.Model).HasMaxLength(120).IsRequired();
            b.Property(x => x.Operation).HasMaxLength(100).IsRequired();
            b.Property(x => x.EstimatedCost).HasPrecision(12, 6);
        });
        modelBuilder.Entity<Notification>(b =>
        {
            b.Property(x => x.Type).HasMaxLength(100).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            b.Property(x => x.DataJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(120).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            b.Property(x => x.DataJson).HasColumnType("jsonb");
            b.Property(x => x.IpAddress).HasMaxLength(64);
        });
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(x => typeof(TenantEntity).IsAssignableFrom(x.ClrType) && !x.ClrType.IsAbstract))
        {
            var row = Expression.Parameter(entityType.ClrType, "row");
            var organizationId = Expression.Property(row, nameof(TenantEntity.OrganizationId));
            var currentOrganizationId = Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId));
            var predicate = Expression.Lambda(Expression.Equal(organizationId, currentOrganizationId), row);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(predicate);
        }
    }
    private static void ConfigureOrganizationRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserRole>().HasOne(x => x.Role).WithMany().HasForeignKey(x => new { x.OrganizationId, x.RoleId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RolePermission>().HasOne(x => x.Role).WithMany().HasForeignKey(x => new { x.OrganizationId, x.RoleId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RolePermission>().HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RefreshToken>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PasswordResetToken>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Department>().HasOne(x => x.ParentDepartment).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ParentDepartmentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDomainRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>().HasOne(x => x.Department).WithMany().HasForeignKey(x => new { x.OrganizationId, x.DepartmentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JobSkill>().HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobSkill>().HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JobDepartment>().HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobDepartment>().HasOne(x => x.Department).WithMany().HasForeignKey(x => new { x.OrganizationId, x.DepartmentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JobRecruiter>().HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobRecruiter>().HasOne(x => x.Recruiter).WithMany().HasForeignKey(x => new { x.OrganizationId, x.RecruiterUserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Candidate>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Resume>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CandidateSkill>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CandidateSkill>().HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CandidateEducation>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CandidateExperience>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecruitmentApplication>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RecruitmentApplication>().HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RecruitmentApplication>().HasOne(x => x.CurrentStage).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CurrentStageId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApplicationStageHistory>().HasOne(x => x.Application).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ApplicationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ApplicationStageHistory>().HasOne(x => x.FromStage).WithMany().HasForeignKey(x => new { x.OrganizationId, x.FromStageId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApplicationStageHistory>().HasOne(x => x.ToStage).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ToStageId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApplicationStageHistory>().HasOne< User >().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ChangedByUserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Interview>().HasOne(x => x.Application).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ApplicationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InterviewParticipant>().HasOne(x => x.Interview).WithMany().HasForeignKey(x => new { x.OrganizationId, x.InterviewId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InterviewParticipant>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InterviewFeedback>().HasOne(x => x.Interview).WithMany().HasForeignKey(x => new { x.OrganizationId, x.InterviewId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InterviewFeedback>().HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => new { x.OrganizationId, x.SubmittedByUserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ResumeParsingResult>().HasOne(x => x.Resume).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ResumeId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CandidateMatchScore>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CandidateMatchScore>().HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AuditLog>().HasOne(x => x.Actor).WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorUserId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureIndexesAndConstraints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("permissions");
            b.Property(x => x.Code).HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasMaxLength(300).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<Skill>(b =>
        {
            b.ToTable("skills");
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.NormalizedName).IsUnique();
        });
        modelBuilder.Entity<User>().HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique().HasDatabaseName("ux_users_org_email");
        modelBuilder.Entity<Role>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(x => new { x.OrganizationId, x.UserId, x.RoleId }).IsUnique();
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.OrganizationId, x.RoleId, x.PermissionId }).IsUnique();
        modelBuilder.Entity<Department>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<PasswordResetToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => new { x.OrganizationId, x.UserId, x.ExpiresAt });
        modelBuilder.Entity<Job>().HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAt });
        modelBuilder.Entity<Job>().HasIndex(x => new { x.OrganizationId, x.DepartmentId, x.Status });
        modelBuilder.Entity<JobSkill>().HasIndex(x => new { x.OrganizationId, x.JobId, x.SkillId }).IsUnique();
        modelBuilder.Entity<JobDepartment>().HasIndex(x => new { x.OrganizationId, x.JobId, x.DepartmentId }).IsUnique();
        modelBuilder.Entity<JobRecruiter>().HasIndex(x => new { x.OrganizationId, x.JobId, x.RecruiterUserId }).IsUnique();
        modelBuilder.Entity<Candidate>().HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");
        modelBuilder.Entity<Resume>().HasIndex(x => new { x.OrganizationId, x.Sha256Hash }).IsUnique();
        modelBuilder.Entity<CandidateSkill>().HasIndex(x => new { x.OrganizationId, x.CandidateId, x.SkillId }).IsUnique();
        modelBuilder.Entity<RecruitmentApplication>().HasIndex(x => new { x.OrganizationId, x.JobId, x.CurrentStageId });
        modelBuilder.Entity<RecruitmentApplication>().HasIndex(x => new { x.OrganizationId, x.CandidateId, x.JobId }).IsUnique();
        modelBuilder.Entity<RecruitmentStage>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        modelBuilder.Entity<RecruitmentStage>().HasIndex(x => new { x.OrganizationId, x.SortOrder }).IsUnique();
        modelBuilder.Entity<ApplicationStageHistory>().HasIndex(x => new { x.OrganizationId, x.ApplicationId, x.ChangedAt });
        modelBuilder.Entity<Interview>().HasIndex(x => new { x.OrganizationId, x.StartsAt });
        modelBuilder.Entity<InterviewParticipant>().HasIndex(x => new { x.OrganizationId, x.InterviewId, x.UserId }).IsUnique();
        modelBuilder.Entity<InterviewFeedback>().ToTable("interview_feedback", t => t.HasCheckConstraint("ck_interview_feedback_rating", "\"Rating\" BETWEEN 1 AND 5"));
        modelBuilder.Entity<ResumeParsingResult>().HasIndex(x => new { x.OrganizationId, x.ResumeId, x.CreatedAt });
        modelBuilder.Entity<CandidateMatchScore>().ToTable("candidate_match_scores", t => t.HasCheckConstraint("ck_candidate_match_score_range", "\"Score\" >= 0 AND \"Score\" <= 100"));
        modelBuilder.Entity<CandidateMatchScore>().HasIndex(x => new { x.OrganizationId, x.JobId, x.Score });
        modelBuilder.Entity<AIProcessingJob>().HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<AIProcessingJob>().HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAt });
        modelBuilder.Entity<AIUsageLog>().HasIndex(x => new { x.OrganizationId, x.RecordedAt });
        modelBuilder.Entity<Notification>().HasIndex(x => new { x.OrganizationId, x.UserId, x.IsRead, x.CreatedAt });
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.OrganizationId, x.EntityType, x.EntityId });
    }

    public override int SaveChanges()
    {
        StampUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampUpdatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
    }
}










