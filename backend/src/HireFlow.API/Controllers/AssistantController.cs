using System.Text.RegularExpressions;
using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController(HireFlowDbContext db) : ControllerBase
{
    [HttpPost("query")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> Query(AssistantQueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Task) || request.Task.Length > 60 || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 1000)
            return BadRequest("Provide a supported task and a question of no more than 1,000 characters.");

        switch (request.Task.Trim().ToLowerInvariant())
        {
            case "find_candidates":
            {
                var skillNames = await db.Skills.AsNoTracking().Where(x => request.Question.ToUpper().Contains(x.NormalizedName)).Select(x => x.NormalizedName).Take(10).ToListAsync(ct);
                var yearsMatch = Regex.Match(request.Question, @"(?<years>\d{1,2}(?:\.\d+)?)\s*\+?\s*years?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                decimal? years = yearsMatch.Success && decimal.TryParse(yearsMatch.Groups["years"].Value, System.Globalization.CultureInfo.InvariantCulture, out var parsedYears) ? parsedYears : null;
                var query = db.Candidates.AsNoTracking();
                if (years is not null) query = query.Where(x => x.TotalExperienceYears >= years);
                if (skillNames.Count > 0) query = query.Where(x => db.CandidateSkills.Where(s => s.CandidateId == x.Id).Join(db.Skills, s => s.SkillId, skill => skill.Id, (s, skill) => skill.NormalizedName).Any(name => skillNames.Contains(name)));
                var candidates = await query.OrderByDescending(x => x.TotalExperienceYears).Take(20)
                    .Select(x => new { x.Id, name = (x.FirstName ?? "") + " " + (x.LastName ?? ""), x.Email, x.Location, x.TotalExperienceYears }).ToListAsync(ct);
                return Ok(new { task = "find_candidates", answer = $"Found {candidates.Count} candidates matching the available criteria.", criteria = new { skills = skillNames, minimumExperienceYears = years }, candidates });
            }
            case "summarize_candidate":
            {
                if (request.CandidateId is not Guid candidateId) return BadRequest("candidateId is required for summarize_candidate.");
                var candidate = await db.Candidates.AsNoTracking().Where(x => x.Id == candidateId).Select(x => new
                {
                    x.Id, x.FirstName, x.LastName, x.ProfessionalSummary, x.TotalExperienceYears,
                    skills = db.CandidateSkills.Where(s => s.CandidateId == x.Id).Join(db.Skills, s => s.SkillId, skill => skill.Id, (s, skill) => skill.Name).ToList(),
                    experience = db.CandidateExperience.Where(e => e.CandidateId == x.Id).Select(e => new { e.JobTitle, e.Company, e.Description }).ToList(),
                    education = db.CandidateEducation.Where(e => e.CandidateId == x.Id).Select(e => new { e.Degree, e.FieldOfStudy, e.Institution }).ToList()
                }).SingleOrDefaultAsync(ct);
                if (candidate is null) return NotFound();
                return Ok(new { task = request.Task, candidate, answer = $"{candidate.FirstName} {candidate.LastName} has {candidate.TotalExperienceYears?.ToString() ?? "unspecified"} years of recorded experience, {candidate.skills.Count} listed skills, and {candidate.experience.Count} experience entries. This summary uses saved profile data only." });
            }
            case "explain_match":
            {
                if (request.CandidateId is not Guid matchCandidateId || request.JobId is not Guid jobId) return BadRequest("candidateId and jobId are required for explain_match.");
                var match = await db.CandidateMatchScores.AsNoTracking().Where(x => x.CandidateId == matchCandidateId && x.JobId == jobId).OrderByDescending(x => x.CreatedAt)
                    .Select(x => new { x.Score, x.AlgorithmVersion, x.MatchedSkillsJson, x.MissingSkillsJson, x.ExplanationJson }).FirstOrDefaultAsync(ct);
                return match is null ? NotFound("No saved match is available. Calculate a match first.") : Ok(new { task = request.Task, match, answer = "This explanation reflects the latest saved, versioned match calculation." });
            }
            case "generate_job_description":
            {
                if (string.IsNullOrWhiteSpace(request.JobTitle) || request.JobTitle.Length > 200) return BadRequest("A jobTitle of no more than 200 characters is required.");
                var skills = request.Skills?.Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray() ?? [];
                var description = $"We are looking for a {request.JobTitle.Trim()} to join our team.\n\nResponsibilities\n- Build, maintain, and improve solutions for this role.\n- Collaborate with teammates and stakeholders.\n- Communicate progress, risks, and technical decisions.\n\nQualifications\n{(skills.Length == 0 ? "- Relevant experience for the role." : string.Join("\n", skills.Select(x => $"- Experience with {x}.")))}\n- Strong problem-solving and communication skills.\n\nPlease review this draft and add team-specific details before publishing.";
                return Ok(new { task = request.Task, jobTitle = request.JobTitle.Trim(), description, generated = "template" });
            }
            default:
                return BadRequest(new { message = "Unsupported task.", supportedTasks = new[] { "find_candidates", "summarize_candidate", "explain_match", "generate_job_description" } });
        }
    }
}

public sealed record AssistantQueryRequest(string Task, string Question, Guid? CandidateId = null, Guid? JobId = null, string? JobTitle = null, IReadOnlyCollection<string>? Skills = null);
