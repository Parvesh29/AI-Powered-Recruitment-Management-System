using System.Globalization;
using System.Text.Json;
using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Application.Matching;
using HireFlow.Domain.Entities.AI;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/matches")]
[Authorize]
public sealed class JobMatchingController(HireFlowDbContext db, ICurrentTenant tenant, IConfiguration configuration) : ControllerBase
{
    [HttpPost("{candidateId:guid}")]
    [RequirePermission("jobs.read")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> Calculate(Guid jobId, Guid candidateId, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null) return NotFound();
        var candidate = await db.Candidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == candidateId, ct);
        if (candidate is null) return NotFound();
        var skillRows = await db.JobSkills.Where(x => x.JobId == jobId)
            .Join(db.Skills, link => link.SkillId, skill => skill.Id, (link, skill) => new { skill.Name, link.IsRequired }).ToListAsync(ct);
        var jobSkills = skillRows.Select(x => (x.Name, x.IsRequired)).ToArray();
        var candidateSkills = await db.CandidateSkills.Where(x => x.CandidateId == candidateId)
            .Join(db.Skills, link => link.SkillId, skill => skill.Id, (link, skill) => skill.Name).ToListAsync(ct);
        var education = await db.CandidateEducation.Where(x => x.CandidateId == candidateId).ToListAsync(ct);
        var experience = await db.CandidateExperience.Where(x => x.CandidateId == candidateId).ToListAsync(ct);
        var primaryResumeId = await db.Resumes.Where(x => x.CandidateId == candidateId && x.IsPrimary).OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (primaryResumeId is Guid resumeId)
        {
            var parsedJson = await db.ResumeParsingResults.Where(x => x.ResumeId == resumeId)
                .OrderByDescending(x => x.CreatedAt).Select(x => x.ParsedDataJson).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(parsedJson))
            {
                using var parsed = JsonDocument.Parse(parsedJson);
                if (parsed.RootElement.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Array)
                {
                    foreach (var project in projects.EnumerateArray())
                    {
                        var name = project.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                        var description = project.TryGetProperty("description", out var descriptionValue) ? descriptionValue.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description)) continue;
                        experience.Add(new HireFlow.Domain.Entities.Candidates.CandidateExperience
                        {
                            Company = "Project",
                            JobTitle = name,
                            Description = description
                        });
                    }
                }
            }
        }
        var weights = new MatchingWeights(ReadWeight("RequiredSkills", 0.50m), ReadWeight("Experience", 0.25m),
            ReadWeight("ProjectRelevance", 0.15m), ReadWeight("Education", 0.10m));
        var evaluation = CandidateMatchingEngine.Evaluate(job, jobSkills, candidate, candidateSkills, education, experience, weights);
        var record = new CandidateMatchScore
        {
            OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, CandidateId = candidateId, JobId = jobId,
            Score = evaluation.Score, MatchedSkillsJson = JsonSerializer.Serialize(evaluation.MatchedSkills),
            MissingSkillsJson = JsonSerializer.Serialize(evaluation.MissingRequiredSkills),
            ExplanationJson = JsonSerializer.Serialize(new { algorithm = evaluation.AlgorithmVersion, categories = evaluation.Categories, warnings = evaluation.DataWarnings }),
            AlgorithmVersion = evaluation.AlgorithmVersion
        };
        db.CandidateMatchScores.Add(record);
        await db.SaveChangesAsync(ct);
        return Ok(new { record.Id, record.JobId, record.CandidateId, record.Score, record.AlgorithmVersion,
            matchedSkills = evaluation.MatchedSkills, missingRequiredSkills = evaluation.MissingRequiredSkills,
            categories = evaluation.Categories, warnings = evaluation.DataWarnings, record.CreatedAt });
    }

    [HttpGet]
    [RequirePermission("jobs.read")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> List(Guid jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("Page must be positive and page size must be between 1 and 100.");
        if (!await db.Jobs.AnyAsync(x => x.Id == jobId, ct)) return NotFound();
        var total = await db.CandidateMatchScores.Where(x => x.JobId == jobId).Select(x => x.CandidateId).Distinct().CountAsync(ct);
        var latestByCandidate = db.CandidateMatchScores.Where(x => x.JobId == jobId)
            .GroupBy(x => x.CandidateId).Select(group => group.OrderByDescending(x => x.CreatedAt).Select(x => x.Id).First());
        var rows = await db.CandidateMatchScores.AsNoTracking().Where(x => latestByCandidate.Contains(x.Id))
            .OrderByDescending(x => x.Score).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.CandidateId, CandidateName = x.Candidate!.FirstName + " " + x.Candidate.LastName,
                x.Score, x.AlgorithmVersion, x.MatchedSkillsJson, x.MissingSkillsJson, x.ExplanationJson, x.CreatedAt }).ToListAsync(ct);
        var result = rows.Select(x => new
        {
            x.Id, x.CandidateId, x.CandidateName, x.Score, x.AlgorithmVersion, x.CreatedAt,
            matchedSkills = JsonDocument.Parse(x.MatchedSkillsJson).RootElement.Clone(),
            missingRequiredSkills = JsonDocument.Parse(x.MissingSkillsJson).RootElement.Clone(),
            explanation = JsonDocument.Parse(x.ExplanationJson).RootElement.Clone()
        });
        return Ok(new { items = result, total, page, pageSize });
    }

    private decimal ReadWeight(string key, decimal fallback) => decimal.TryParse(configuration[$"Matching:Weights:{key}"],
        NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

