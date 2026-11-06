using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Recruitment;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using HireFlow.API.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/interviews")]
[Authorize]
public sealed class InterviewsController(HireFlowDbContext db, ICurrentTenant tenant, IHubContext<NotificationsHub> hub) : ControllerBase
{
    [HttpGet]
    [RequirePermission("interviews.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var interviews = await db.Interviews.AsNoTracking().OrderBy(x => x.StartsAt).Select(x => new
        {
            x.Id, x.ApplicationId, x.Type, x.RoundNumber, x.StartsAt, x.EndsAt, x.MeetingUrl, x.Status,
            candidateName = (x.Application!.Candidate!.FirstName ?? "") + " " + (x.Application.Candidate.LastName ?? ""),
            jobTitle = x.Application.Job!.Title,
            participants = db.InterviewParticipants.Where(p => p.InterviewId == x.Id).Join(db.Users, p => p.UserId, u => u.Id, (p, u) => new { u.Id, u.FirstName, u.LastName, u.Email }).ToList(),
            feedbackCount = db.InterviewFeedback.Count(f => f.InterviewId == x.Id)
        }).ToListAsync(ct);
        return Ok(interviews);
    }

    [HttpPost]
    [RequirePermission("interviews.create")]
    public async Task<IActionResult> Schedule(ScheduleInterviewRequest request, CancellationToken ct)
    {
        if (request.StartsAt.Offset != TimeSpan.Zero || request.EndsAt.Offset != TimeSpan.Zero || request.EndsAt <= request.StartsAt || request.EndsAt - request.StartsAt > TimeSpan.FromHours(8))
            return BadRequest("Provide UTC start and end times with a positive duration no longer than eight hours.");
        if (request.RoundNumber is < 1 or > 20 || request.ParticipantIds is null || request.ParticipantIds.Count is < 1 or > 20 || request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count)
            return BadRequest("Interview round and participant list are invalid.");
        if (request.MeetingUrl is { Length: > 2000 } || (!string.IsNullOrWhiteSpace(request.MeetingUrl) &&
            (!Uri.TryCreate(request.MeetingUrl, UriKind.Absolute, out var meetingUri) || meetingUri.Scheme != Uri.UriSchemeHttps)))
            return BadRequest("Meeting URL must be a valid HTTPS URL of at most 2,000 characters.");
        if (!await db.Applications.AnyAsync(x => x.Id == request.ApplicationId, ct)) return NotFound("Application was not found.");
        var users = await db.Users.Where(x => request.ParticipantIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        if (users.Count != request.ParticipantIds.Count) return BadRequest("Every interviewer must be an active user in this organization.");
        if (await db.InterviewParticipants.Where(x => request.ParticipantIds.Contains(x.UserId))
            .AnyAsync(p => db.Interviews.Any(i => i.Id == p.InterviewId && i.Status == "Scheduled" && i.StartsAt < request.EndsAt && i.EndsAt > request.StartsAt), ct))
            return Conflict("An interviewer already has a scheduled interview during this time.");

        var interview = new Interview { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, ApplicationId = request.ApplicationId,
            Type = request.Type, RoundNumber = request.RoundNumber, StartsAt = request.StartsAt, EndsAt = request.EndsAt,
            MeetingUrl = string.IsNullOrWhiteSpace(request.MeetingUrl) ? null : request.MeetingUrl.Trim() };
        db.Interviews.Add(interview);
        foreach (var userId in users) db.InterviewParticipants.Add(new InterviewParticipant { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, InterviewId = interview.Id, UserId = userId });
        await db.SaveChangesAsync(ct);
        foreach (var userId in users)
            await NotificationDispatcher.SendAsync(db, hub, tenant.OrganizationId, userId, "interview_scheduled", "Interview scheduled", $"You are assigned to an interview on {interview.StartsAt:yyyy-MM-dd HH:mm} UTC.", new { interviewId = interview.Id, applicationId = interview.ApplicationId }, ct);
        return CreatedAtAction(nameof(List), new { }, new { interview.Id, interview.ApplicationId, interview.Type, interview.RoundNumber, interview.StartsAt, interview.EndsAt, interview.Status });
    }

    [HttpPut("{interviewId:guid}/schedule")]
    [RequirePermission("interviews.create")]
    public async Task<IActionResult> Reschedule(Guid interviewId, RescheduleInterviewRequest request, CancellationToken ct)
    {
        if (request.StartsAt.Offset != TimeSpan.Zero || request.EndsAt.Offset != TimeSpan.Zero || request.EndsAt <= request.StartsAt || request.EndsAt - request.StartsAt > TimeSpan.FromHours(8))
            return BadRequest("Provide UTC start and end times with a positive duration no longer than eight hours.");
        if (request.MeetingUrl is { Length: > 2000 } || (!string.IsNullOrWhiteSpace(request.MeetingUrl) &&
            (!Uri.TryCreate(request.MeetingUrl, UriKind.Absolute, out var meetingUri) || meetingUri.Scheme != Uri.UriSchemeHttps)))
            return BadRequest("Meeting URL must be a valid HTTPS URL of at most 2,000 characters.");
        var interview = await db.Interviews.SingleOrDefaultAsync(x => x.Id == interviewId, ct);
        if (interview is null) return NotFound();
        if (interview.Status != "Scheduled") return Conflict("Only scheduled interviews can be rescheduled.");
        var participantIds = await db.InterviewParticipants.Where(x => x.InterviewId == interviewId).Select(x => x.UserId).ToListAsync(ct);
        if (await db.InterviewParticipants.Where(x => x.InterviewId != interviewId && participantIds.Contains(x.UserId))
            .AnyAsync(p => db.Interviews.Any(i => i.Id == p.InterviewId && i.Status == "Scheduled" && i.StartsAt < request.EndsAt && i.EndsAt > request.StartsAt), ct))
            return Conflict("An interviewer already has a scheduled interview during this time.");
        interview.StartsAt = request.StartsAt;
        interview.EndsAt = request.EndsAt;
        interview.MeetingUrl = request.MeetingUrl?.Trim();
        await db.SaveChangesAsync(ct);
        foreach (var userId in participantIds)
            await NotificationDispatcher.SendAsync(db, hub, tenant.OrganizationId, userId, "interview_rescheduled", "Interview rescheduled", $"An interview was rescheduled to {interview.StartsAt:yyyy-MM-dd HH:mm} UTC.", new { interviewId }, ct);
        return Ok(new { interview.Id, interview.StartsAt, interview.EndsAt, interview.MeetingUrl });
    }

    [HttpPost("{interviewId:guid}/feedback")]
    [RequirePermission("interviews.feedback")]
    public async Task<IActionResult> SubmitFeedback(Guid interviewId, SubmitInterviewFeedbackRequest request, CancellationToken ct)
    {
        if (request.Rating is < 1 or > 5 || request.Strengths?.Length > 5000 || request.Concerns?.Length > 5000 || request.Notes?.Length > 10000)
            return BadRequest("Feedback fields are invalid; rating must be between one and five.");
        var interview = await db.Interviews.SingleOrDefaultAsync(x => x.Id == interviewId, ct);
        if (interview is null) return NotFound();
        if (!await db.InterviewParticipants.AnyAsync(x => x.InterviewId == interviewId && x.UserId == tenant.UserId, ct)) return Forbid();
        if (await db.InterviewFeedback.AnyAsync(x => x.InterviewId == interviewId && x.SubmittedByUserId == tenant.UserId, ct)) return Conflict("You have already submitted feedback for this interview.");
        var feedback = new InterviewFeedback { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, InterviewId = interviewId, SubmittedByUserId = tenant.UserId,
            Rating = request.Rating, Recommendation = request.Recommendation, Strengths = request.Strengths?.Trim(), Concerns = request.Concerns?.Trim(), Notes = request.Notes?.Trim() };
        db.InterviewFeedback.Add(feedback);
        await db.SaveChangesAsync(ct);
        var otherParticipants = await db.InterviewParticipants.Where(x => x.InterviewId == interviewId && x.UserId != tenant.UserId).Select(x => x.UserId).ToListAsync(ct);
        foreach (var userId in otherParticipants)
            await NotificationDispatcher.SendAsync(db, hub, tenant.OrganizationId, userId, "interview_feedback_submitted", "Interview feedback submitted", "An interviewer submitted feedback for a scheduled interview.", new { interviewId }, ct);
        return Ok(new { feedback.Id, feedback.InterviewId, feedback.Rating, feedback.Recommendation, feedback.SubmittedAt });
    }

    [HttpGet("{interviewId:guid}/feedback")]
    [RequirePermission("interviews.read")]
    public async Task<IActionResult> Feedback(Guid interviewId, CancellationToken ct)
    {
        if (!await db.Interviews.AnyAsync(x => x.Id == interviewId, ct)) return NotFound();
        var feedback = await db.InterviewFeedback.AsNoTracking().Where(x => x.InterviewId == interviewId).OrderBy(x => x.SubmittedAt)
            .Select(x => new { x.Id, x.SubmittedByUserId, reviewer = x.SubmittedBy!.FirstName + " " + x.SubmittedBy.LastName, x.Rating, x.Recommendation, x.Strengths, x.Concerns, x.Notes, x.SubmittedAt }).ToListAsync(ct);
        return Ok(feedback);
    }
}

public sealed record ScheduleInterviewRequest(Guid ApplicationId, InterviewType Type, int RoundNumber, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? MeetingUrl, IReadOnlyCollection<Guid> ParticipantIds);
public sealed record RescheduleInterviewRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? MeetingUrl);
public sealed record SubmitInterviewFeedbackRequest(int Rating, InterviewRecommendation Recommendation, string? Strengths, string? Concerns, string? Notes);
