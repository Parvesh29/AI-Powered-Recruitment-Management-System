using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.AI;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Jobs;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using HireFlow.API.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace HireFlow.API.Jobs;

public sealed class ResumeParsingBackgroundJob(
    HireFlowDbContext db,
    IResumeFileStorage storage,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHubContext<NotificationsHub> hub)
{
    public async Task ProcessAsync(Guid organizationId, Guid resumeId, Guid requestedBy, Guid processingJobId)
    {
        var job = await db.AIProcessingJobs.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
            x.Id == processingJobId && x.OrganizationId == organizationId && x.JobType == "resume_parse" && x.IdempotencyKey == $"resume:{resumeId:N}");
        var resume = await db.Resumes.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == resumeId && x.OrganizationId == organizationId);
        if (job is null || resume is null || job.Status == BackgroundJobStatus.Completed) return;

        job.Status = BackgroundJobStatus.Processing;
        job.AttemptCount++;
        job.ErrorMessage = null;
        resume.ProcessingStatus = ResumeProcessingStatus.Processing;
        await db.SaveChangesAsync();

        ResumeParsingResult result;
        try
        {
            await using var stream = await storage.OpenReadAsync(resume.StorageKey, CancellationToken.None);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/v1/resume/parse?filename={Uri.EscapeDataString(resume.OriginalFileName)}") { Content = new StreamContent(stream) };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(resume.ContentType);
            var baseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
            using var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
            var rawJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
            using var parsed = JsonDocument.Parse(rawJson);
            var status = parsed.RootElement.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
            var succeeded = response.IsSuccessStatusCode && status == "completed";
            resume.ProcessingStatus = succeeded ? ResumeProcessingStatus.Completed : ResumeProcessingStatus.Failed;
            if (succeeded) await ApplyExtractedProfileAsync(organizationId, resume.CandidateId, parsed.RootElement);
            result = new ResumeParsingResult
            {
                OrganizationId = organizationId, CreatedBy = requestedBy, ResumeId = resumeId, Status = resume.ProcessingStatus,
                ParserVersion = parsed.RootElement.TryGetProperty("parser_version", out var version) ? version.GetString() ?? "unknown" : "unknown",
                ParsedDataJson = parsed.RootElement.GetRawText(), ProcessedAt = DateTimeOffset.UtcNow,
                ErrorCode = succeeded ? null : status == "needs_ocr" ? "ocr_required" : status == "empty" ? "empty_document" : "parser_failed",
                ErrorMessage = succeeded ? null : status == "needs_ocr" ? "No selectable text was found; OCR is needed." : status == "empty" ? "The document contains no extractable text." : "The parser did not complete this document."
            };
        }
        catch (HttpRequestException)
        {
            result = Failure(organizationId, requestedBy, resumeId, "parser_unavailable", "The resume parser is unavailable.");
            resume.ProcessingStatus = ResumeProcessingStatus.Failed;
        }
        catch (JsonException)
        {
            result = Failure(organizationId, requestedBy, resumeId, "invalid_parser_response", "The parser returned invalid structured data.");
            resume.ProcessingStatus = ResumeProcessingStatus.Failed;
        }
        catch (IOException)
        {
            result = Failure(organizationId, requestedBy, resumeId, "resume_storage_unavailable", "The stored resume could not be read.");
            resume.ProcessingStatus = ResumeProcessingStatus.Failed;
        }

        db.ResumeParsingResults.Add(result);
        job.Status = result.Status == ResumeProcessingStatus.Completed ? BackgroundJobStatus.Completed : BackgroundJobStatus.Failed;
        job.ErrorMessage = result.ErrorMessage;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await NotificationDispatcher.SendAsync(db, hub, organizationId, requestedBy,
            result.Status == ResumeProcessingStatus.Completed ? "resume_processing_completed" : "resume_processing_failed",
            result.Status == ResumeProcessingStatus.Completed ? "Resume processing complete" : "Resume processing failed",
            result.ErrorMessage ?? "The resume was parsed and is ready to review.", new { resumeId }, CancellationToken.None);
    }

    private async Task ApplyExtractedProfileAsync(Guid organizationId, Guid candidateId, JsonElement parsed)
    {
        var candidate = await db.Candidates.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == candidateId && x.OrganizationId == organizationId);
        if (candidate is null) return;
        if (parsed.TryGetProperty("total_experience", out var totalExperience) && totalExperience.ValueKind == JsonValueKind.Number && totalExperience.TryGetDecimal(out var years))
            candidate.TotalExperienceYears = Math.Round(years, 2);

        if (parsed.TryGetProperty("skills", out var skillValues) && skillValues.ValueKind == JsonValueKind.Array)
        {
            var names = skillValues.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var normalized = names.Select(x => x.ToUpperInvariant()).ToArray();
            var skills = await db.Skills.Where(x => normalized.Contains(x.NormalizedName)).ToListAsync();
            var found = skills.Select(x => x.NormalizedName).ToHashSet(StringComparer.Ordinal);
            var addedSkills = names.Where(x => !found.Contains(x.ToUpperInvariant())).Select(x => new Skill { Name = x, NormalizedName = x.ToUpperInvariant() }).ToArray();
            db.Skills.AddRange(addedSkills);
            skills.AddRange(addedSkills);
            var existingLinks = await db.CandidateSkills.IgnoreQueryFilters().Where(x => x.OrganizationId == organizationId && x.CandidateId == candidateId).Select(x => x.SkillId).ToListAsync();
            var linked = existingLinks.ToHashSet();
            foreach (var skill in skills.Where(x => linked.Add(x.Id)))
                db.CandidateSkills.Add(new CandidateSkill { OrganizationId = organizationId, CandidateId = candidateId, SkillId = skill.Id });
        }

        if (parsed.TryGetProperty("education", out var educationValues) && educationValues.ValueKind == JsonValueKind.Array)
        {
            var current = await db.CandidateEducation.IgnoreQueryFilters().Where(x => x.OrganizationId == organizationId && x.CandidateId == candidateId).ToListAsync();
            foreach (var item in educationValues.EnumerateArray())
            {
                var institution = ReadString(item, "institution");
                if (string.IsNullOrWhiteSpace(institution) || institution.Length > 250) continue;
                var degree = ReadString(item, "degree");
                if (current.Any(x => x.Institution.Equals(institution, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(x.Degree, degree, StringComparison.OrdinalIgnoreCase))) continue;
                var entry = new CandidateEducation { OrganizationId = organizationId, CandidateId = candidateId,
                    Institution = institution, Degree = degree, FieldOfStudy = ReadString(item, "field_of_study") };
                db.CandidateEducation.Add(entry);
                current.Add(entry);
            }
        }

        if (parsed.TryGetProperty("experience", out var experienceValues) && experienceValues.ValueKind == JsonValueKind.Array)
        {
            var current = await db.CandidateExperience.IgnoreQueryFilters().Where(x => x.OrganizationId == organizationId && x.CandidateId == candidateId).ToListAsync();
            foreach (var item in experienceValues.EnumerateArray())
            {
                var company = ReadString(item, "company");
                var title = ReadString(item, "job_title");
                if (string.IsNullOrWhiteSpace(company) || company.Length > 250) continue;
                if (current.Any(x => x.Company.Equals(company, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(x.JobTitle, title, StringComparison.OrdinalIgnoreCase))) continue;
                var (start, end, isCurrent) = ParseDateRange(ReadString(item, "date_range"));
                var entry = new CandidateExperience { OrganizationId = organizationId, CandidateId = candidateId,
                    Company = company, JobTitle = title, StartDate = start, EndDate = end, IsCurrent = isCurrent };
                db.CandidateExperience.Add(entry);
                current.Add(entry);
            }
        }
    }

    private static string? ReadString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private static (DateOnly? Start, DateOnly? End, bool IsCurrent) ParseDateRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null, false);
        var match = Regex.Match(value, @"^\s*(?<start>.+?)\s*[-–—]\s*(?<end>Present|Current|.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return (null, null, false);
        var start = ParseMonthYear(match.Groups["start"].Value);
        var isCurrent = match.Groups["end"].Value.Equals("Present", StringComparison.OrdinalIgnoreCase) || match.Groups["end"].Value.Equals("Current", StringComparison.OrdinalIgnoreCase);
        var end = isCurrent ? null : ParseMonthYear(match.Groups["end"].Value);
        return (start, end, isCurrent);
    }

    private static DateOnly? ParseMonthYear(string value)
    {
        var formats = new[] { "MMM yyyy", "MMMM yyyy", "MMM. yyyy", "MMMM. yyyy", "yyyy" };
        if (!DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
        return new DateOnly(date.Year, date.Month, 1);
    }

    private static ResumeParsingResult Failure(Guid organizationId, Guid requestedBy, Guid resumeId, string code, string message) => new()
    {
        OrganizationId = organizationId, CreatedBy = requestedBy, ResumeId = resumeId, Status = ResumeProcessingStatus.Failed,
        ParserVersion = "unknown", ParsedDataJson = "{}", ProcessedAt = DateTimeOffset.UtcNow, ErrorCode = code, ErrorMessage = message
    };
}
