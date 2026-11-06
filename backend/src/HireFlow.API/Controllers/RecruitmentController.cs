using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Recruitment;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using HireFlow.API.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitmentApplication = HireFlow.Domain.Entities.Recruitment.Application;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public sealed class RecruitmentController(HireFlowDbContext db, ICurrentTenant tenant, IHubContext<NotificationsHub> hub) : ControllerBase
{
    private static readonly string[] DefaultStages = ["Applied", "ResumeScreening", "Shortlisted", "Interview", "Selected", "Rejected", "Hired"];

    [HttpGet]
    [RequirePermission("applications.read")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("Page must be positive and page size between 1 and 100.");
        await EnsureStagesAsync(ct);
        var query = db.Applications.AsNoTracking().OrderByDescending(x => x.AppliedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
        {
            x.Id, x.CandidateId, candidateName = (x.Candidate!.FirstName ?? "") + " " + (x.Candidate.LastName ?? ""), x.Candidate.Email,
            x.JobId, jobTitle = x.Job!.Title, x.Source, x.AppliedAt, currentStageId = x.CurrentStageId, stage = x.CurrentStage!.Name
        }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize, stages = await db.RecruitmentStages.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.SortOrder, x.IsTerminal }).ToListAsync(ct) });
    }

    [HttpPost]
    [RequirePermission("applications.update")]
    public async Task<IActionResult> Create(CreateApplicationRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.AnyAsync(x => x.Id == request.CandidateId, ct);
        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == request.JobId, ct);
        if (!candidate || job is null) return NotFound("Candidate or job was not found in this organization.");
        if (job.Status != JobStatus.Open) return Conflict("Applications can only be created for open jobs.");
        if (await db.Applications.AnyAsync(x => x.CandidateId == request.CandidateId && x.JobId == request.JobId, ct)) return Conflict("This candidate already has an application for this job.");
        await EnsureStagesAsync(ct);
        var first = await db.RecruitmentStages.SingleAsync(x => x.Name == "Applied", ct);
        var application = new RecruitmentApplication { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, CandidateId = request.CandidateId, JobId = request.JobId, CurrentStageId = first.Id, Source = request.Source };
        db.Applications.Add(application);
        db.ApplicationStageHistory.Add(new ApplicationStageHistory { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, ApplicationId = application.Id, ToStageId = first.Id, ChangedByUserId = tenant.UserId, Notes = "Application created." });
        await db.SaveChangesAsync(ct);
        var recruiterIds = await db.JobRecruiters.Where(x => x.JobId == job.Id).Select(x => x.RecruiterUserId).ToListAsync(ct);
        foreach (var recruiterId in recruiterIds)
            await NotificationDispatcher.SendAsync(db, hub, tenant.OrganizationId, recruiterId, "application_received", "New application", "A candidate applied to one of your jobs.", new { applicationId = application.Id, jobId = job.Id }, ct);
        return CreatedAtAction(nameof(List), new { }, new { application.Id, application.CandidateId, application.JobId, stage = first.Name, application.AppliedAt });
    }

    [HttpGet("{applicationId:guid}/history")]
    [RequirePermission("applications.read")]
    public async Task<IActionResult> History(Guid applicationId, CancellationToken ct)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == applicationId, ct)) return NotFound();
        var history = await db.ApplicationStageHistory.AsNoTracking().Where(x => x.ApplicationId == applicationId).OrderBy(x => x.ChangedAt)
            .Select(x => new { x.Id, x.FromStageId, from = x.FromStage == null ? null : x.FromStage.Name, x.ToStageId, to = x.ToStage!.Name, x.ChangedByUserId, x.ChangedAt, x.Notes }).ToListAsync(ct);
        return Ok(history);
    }

    [HttpPut("{applicationId:guid}/stage")]
    [RequirePermission("applications.update")]
    public async Task<IActionResult> Move(Guid applicationId, MoveApplicationRequest request, CancellationToken ct)
    {
        var application = await db.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (application is null) return NotFound();
        var stages = await db.RecruitmentStages.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var current = stages.Single(x => x.Id == application.CurrentStageId);
        var next = stages.SingleOrDefault(x => x.Id == request.StageId);
        if (next is null) return BadRequest("The requested stage is not active.");
        if (!IsValidTransition(current.Name, next.Name)) return Conflict($"The transition from {current.Name} to {next.Name} is not allowed.");
        application.CurrentStageId = next.Id;
        db.ApplicationStageHistory.Add(new ApplicationStageHistory { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, ApplicationId = application.Id,
            FromStageId = current.Id, ToStageId = next.Id, ChangedByUserId = tenant.UserId, Notes = request.Notes?.Trim() });
        await db.SaveChangesAsync(ct);
        if (next.Name == "Shortlisted")
        {
            var recruiterIds = await db.JobRecruiters.Where(x => x.JobId == application.JobId).Select(x => x.RecruiterUserId).ToListAsync(ct);
            foreach (var recruiterId in recruiterIds)
                await NotificationDispatcher.SendAsync(db, hub, tenant.OrganizationId, recruiterId, "candidate_shortlisted", "Candidate shortlisted", "An application moved to the shortlisted stage.", new { applicationId = application.Id }, ct);
        }
        return Ok(new { application.Id, stageId = next.Id, stage = next.Name });
    }

    private async Task EnsureStagesAsync(CancellationToken ct)
    {
        if (await db.RecruitmentStages.AnyAsync(ct)) return;
        for (var index = 0; index < DefaultStages.Length; index++)
            db.RecruitmentStages.Add(new RecruitmentStage { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, Name = DefaultStages[index], SortOrder = index, IsTerminal = DefaultStages[index] is "Rejected" or "Hired" });
        await db.SaveChangesAsync(ct);
    }

    private static bool IsValidTransition(string from, string to) => (from, to) switch
    {
        ("Applied", "ResumeScreening" or "Rejected") => true,
        ("ResumeScreening", "Shortlisted" or "Rejected") => true,
        ("Shortlisted", "Interview" or "Rejected") => true,
        ("Interview", "Selected" or "Rejected") => true,
        ("Selected", "Hired" or "Rejected") => true,
        _ => false
    };
}

public sealed record CreateApplicationRequest(Guid CandidateId, Guid JobId, CandidateSource Source = CandidateSource.Other);
public sealed record MoveApplicationRequest(Guid StageId, string? Notes);
