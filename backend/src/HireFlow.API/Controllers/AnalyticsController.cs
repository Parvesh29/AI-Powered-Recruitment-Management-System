using System.Text;
using HireFlow.API.Authorization;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public sealed class AnalyticsController(HireFlowDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    [RequirePermission("analytics.read")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var totalCandidates = await db.Candidates.CountAsync(ct);
        var totalApplications = await db.Applications.CountAsync(ct);
        var openJobs = await db.Jobs.CountAsync(x => x.Status == JobStatus.Open, ct);
        var shortlisted = await db.Applications.CountAsync(x => x.CurrentStage!.Name == "Shortlisted", ct);
        var interviewed = await db.Interviews.Select(x => x.ApplicationId).Distinct().CountAsync(ct);
        var hired = await db.Applications.CountAsync(x => x.CurrentStage!.Name == "Hired", ct);
        var rejected = await db.Applications.CountAsync(x => x.CurrentStage!.Name == "Rejected", ct);
        var hiredRows = await db.Applications.Where(x => x.CurrentStage!.Name == "Hired")
            .Select(x => new { x.AppliedAt, HiredAt = db.ApplicationStageHistory.Where(h => h.ApplicationId == x.Id && h.ToStage!.Name == "Hired").Select(h => (DateTimeOffset?)h.ChangedAt).Min() }).ToListAsync(ct);
        var averageTimeToHireDays = hiredRows.Where(x => x.HiredAt.HasValue).Select(x => (x.HiredAt!.Value - x.AppliedAt).TotalDays).DefaultIfEmpty(0).Average();
        var byJob = await db.Applications.GroupBy(x => x.Job!.Title).Select(g => new { job = g.Key, applications = g.Count() }).OrderByDescending(x => x.applications).Take(12).ToListAsync(ct);
        var stageRows = await db.Applications.GroupBy(x => x.CurrentStage!.Name).Select(g => new { stage = g.Key, count = g.Count() }).ToListAsync(ct);
        var since = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
        var monthlyRows = await db.Applications.Where(x => x.AppliedAt >= since).Select(x => x.AppliedAt).ToListAsync(ct);
        var monthly = Enumerable.Range(0, 12).Select(offset => since.AddMonths(offset)).Select(month => new
        {
            month = month.ToString("yyyy-MM"), applications = monthlyRows.Count(date => date.Year == month.Year && date.Month == month.Month),
            hired = hiredRows.Count(row => row.HiredAt is DateTimeOffset date && date.Year == month.Year && date.Month == month.Month)
        }).ToArray();
        var recruiterJobRows = await db.JobRecruiters.AsNoTracking().Select(x => new
        {
            x.RecruiterUserId, recruiter = x.Recruiter!.FirstName + " " + x.Recruiter.LastName, x.JobId,
            applications = db.Applications.Count(a => a.JobId == x.JobId)
        }).ToListAsync(ct);
        var recruiterPerformance = recruiterJobRows.GroupBy(x => new { x.RecruiterUserId, x.recruiter })
            .Select(g => new { recruiter = g.Key.recruiter, jobsAssigned = g.Count(), applications = g.Sum(x => x.applications) })
            .OrderByDescending(x => x.applications).Take(10).ToArray();
        return Ok(new
        {
            kpis = new { totalCandidates, totalApplications, openJobs, shortlisted, interviewed, hired, rejected,
                hiringConversionRate = totalApplications == 0 ? 0 : Math.Round((decimal)hired / totalApplications * 100, 1), averageTimeToHireDays = Math.Round(averageTimeToHireDays, 1) },
            applicationsByJob = byJob, recruitmentFunnel = stageRows, monthlyHiringTrends = monthly, recruiterPerformance
        });
    }

    [HttpGet("applications.csv")]
    [RequirePermission("analytics.read")]
    public async Task<IActionResult> ExportApplications(CancellationToken ct)
    {
        var rows = await db.Applications.AsNoTracking().OrderByDescending(x => x.AppliedAt).Select(x => new
        {
            candidate = (x.Candidate!.FirstName ?? "") + " " + (x.Candidate.LastName ?? ""), x.Candidate.Email, job = x.Job!.Title, stage = x.CurrentStage!.Name, x.Source, x.AppliedAt
        }).ToListAsync(ct);
        var csv = new StringBuilder("candidate,email,job,stage,source,applied_at_utc\r\n");
        foreach (var row in rows) csv.AppendLine(string.Join(',', new[] { row.candidate, row.Email ?? "", row.job, row.stage, row.Source.ToString(), row.AppliedAt.ToString("O") }.Select(Csv)));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "hireflow-applications.csv");
    }

    private static string Csv(string value)
    {
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
