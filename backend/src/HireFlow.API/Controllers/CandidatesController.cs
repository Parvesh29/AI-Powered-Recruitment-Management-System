using System.IO.Compression;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text.Json;
using HireFlow.Domain.Entities.AI;
using HireFlow.API.Authorization;
using HireFlow.API.Jobs;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Jobs;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize]
public sealed class CandidatesController(HireFlowDbContext db, ICurrentTenant tenant, IResumeFileStorage storage, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    private const long MaximumResumeBytes = 10 * 1024 * 1024;

    [HttpGet]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> Search([FromQuery] CandidateSearchRequest request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100) return BadRequest("Page must be positive and page size must be between 1 and 100.");
        var query = db.Candidates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => (x.FirstName != null && x.FirstName.Contains(term)) || (x.LastName != null && x.LastName.Contains(term)) || (x.Email != null && x.Email.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(request.Location)) query = query.Where(x => x.Location != null && x.Location.Contains(request.Location.Trim()));
        if (request.MinimumExperience is decimal minimum) query = query.Where(x => x.TotalExperienceYears >= minimum);
        if (request.MaximumExperience is decimal maximum) query = query.Where(x => x.TotalExperienceYears <= maximum);
        if (!string.IsNullOrWhiteSpace(request.Skill))
        {
            var skill = request.Skill.Trim().ToUpperInvariant();
            query = query.Where(candidate => db.CandidateSkills.Where(x => x.CandidateId == candidate.Id)
                .Join(db.Skills, x => x.SkillId, s => s.Id, (x, s) => s.NormalizedName).Any(name => name.Contains(skill)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new CandidateListItem(x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.Location, x.TotalExperienceYears,
                db.Resumes.Where(r => r.CandidateId == x.Id).OrderByDescending(r => r.IsPrimary).Select(r => (Guid?)r.Id).FirstOrDefault()))
            .ToListAsync(ct);
        return Ok(new CandidatePagedResult<CandidateListItem>(items, total, request.Page, request.PageSize));
    }

    [HttpGet("{candidateId:guid}")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> Get(Guid candidateId, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking().Where(x => x.Id == candidateId).Select(x => new
        {
            x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.Location, x.ProfessionalSummary, x.TotalExperienceYears, x.CreatedAt, x.UpdatedAt,
            Skills = db.CandidateSkills.Where(s => s.CandidateId == x.Id).Join(db.Skills, s => s.SkillId, skill => skill.Id,
                (s, skill) => new CandidateSkillDto(skill.Id, skill.Name, s.YearsExperience)).ToList(),
            Education = db.CandidateEducation.Where(e => e.CandidateId == x.Id).OrderByDescending(e => e.EndYear).Select(e => new EducationDto(e.Id, e.Institution, e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear)).ToList(),
            Experience = db.CandidateExperience.Where(e => e.CandidateId == x.Id).OrderByDescending(e => e.StartDate).Select(e => new ExperienceDto(e.Id, e.Company, e.JobTitle, e.Description, e.StartDate, e.EndDate, e.IsCurrent)).ToList(),
            Resumes = db.Resumes.Where(r => r.CandidateId == x.Id).OrderByDescending(r => r.CreatedAt).Select(r => new ResumeDto(r.Id, r.OriginalFileName, r.ContentType, r.SizeBytes, r.IsPrimary, r.ProcessingStatus, r.CreatedAt)).ToList()
        }).SingleOrDefaultAsync(ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    [RequirePermission("candidates.create")]
    public async Task<IActionResult> Create(CreateCandidateRequest request, CancellationToken ct)
    {
        var error = ValidateCandidate(request.FirstName, request.LastName, request.Email, request.TotalExperienceYears, request.Skills, request.Education, request.Experience);
        if (error is not null) return BadRequest(error);
        var normalized = NormalizeEmail(request.Email);
        if (normalized is not null)
        {
            var duplicate = await db.Candidates.Where(x => x.NormalizedEmail == normalized).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (duplicate is Guid existingId) return Conflict(new { message = "A candidate with this email already exists in this organization.", candidateId = existingId });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var candidate = new Candidate
        {
            OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, FirstName = Clean(request.FirstName), LastName = Clean(request.LastName),
            Email = Clean(request.Email)?.ToLowerInvariant(), NormalizedEmail = normalized, PhoneNumber = Clean(request.PhoneNumber),
            Location = Clean(request.Location), ProfessionalSummary = Clean(request.ProfessionalSummary), TotalExperienceYears = request.TotalExperienceYears
        };
        db.Candidates.Add(candidate);
        await ReplaceSkillsAsync(candidate, request.Skills, ct);
        AddEducation(candidate, request.Education);
        AddExperience(candidate, request.Experience);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Conflict("Candidate conflicts with an existing organization record."); }
        return CreatedAtAction(nameof(Get), new { candidateId = candidate.Id }, new { candidate.Id, candidate.FirstName, candidate.LastName, candidate.Email });
    }

    [HttpPut("{candidateId:guid}")]
    [RequirePermission("candidates.update")]
    public async Task<IActionResult> Update(Guid candidateId, UpdateCandidateRequest request, CancellationToken ct)
    {
        var error = ValidateCandidate(request.FirstName, request.LastName, request.Email, request.TotalExperienceYears, request.Skills, request.Education, request.Experience);
        if (error is not null) return BadRequest(error);
        var candidate = await db.Candidates.SingleOrDefaultAsync(x => x.Id == candidateId, ct);
        if (candidate is null) return NotFound();
        var normalized = NormalizeEmail(request.Email);
        if (normalized is not null && await db.Candidates.AnyAsync(x => x.Id != candidateId && x.NormalizedEmail == normalized, ct))
            return Conflict("A candidate with this email already exists in this organization.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        candidate.FirstName = Clean(request.FirstName); candidate.LastName = Clean(request.LastName); candidate.Email = Clean(request.Email)?.ToLowerInvariant();
        candidate.NormalizedEmail = normalized; candidate.PhoneNumber = Clean(request.PhoneNumber); candidate.Location = Clean(request.Location);
        candidate.ProfessionalSummary = Clean(request.ProfessionalSummary); candidate.TotalExperienceYears = request.TotalExperienceYears;
        await ReplaceSkillsAsync(candidate, request.Skills, ct);
        var education = await db.CandidateEducation.Where(x => x.CandidateId == candidateId).ToListAsync(ct); db.CandidateEducation.RemoveRange(education);
        var experience = await db.CandidateExperience.Where(x => x.CandidateId == candidateId).ToListAsync(ct); db.CandidateExperience.RemoveRange(experience);
        AddEducation(candidate, request.Education); AddExperience(candidate, request.Experience);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Conflict("Candidate conflicts with an existing organization record."); }
        return Ok(new { candidate.Id, candidate.FirstName, candidate.LastName, candidate.Email, candidate.UpdatedAt });
    }

    [HttpPost("{candidateId:guid}/resumes")]
    [RequirePermission("candidates.update")]
    [RequestSizeLimit(MaximumResumeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadResume(Guid candidateId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length is <= 0 or > MaximumResumeBytes) return BadRequest("Choose a PDF or DOCX resume no larger than 10 MB.");
        var candidate = await db.Candidates.SingleOrDefaultAsync(x => x.Id == candidateId, ct);
        if (candidate is null) return NotFound();
        if (await db.Resumes.CountAsync(x => x.CandidateId == candidateId, ct) >= 5) return Conflict("A candidate can have at most five stored resumes.");
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        var expectedType = extension switch { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", _ => null };
        if (expectedType is null || !string.Equals(file.ContentType, expectedType, StringComparison.OrdinalIgnoreCase)) return BadRequest("Only PDF and DOCX files with a matching content type are accepted.");
        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (!HasValidSignature(bytes, extension)) return BadRequest("The uploaded file signature does not match its file type.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var duplicate = await db.Resumes.Where(x => x.Sha256Hash == hash).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (duplicate is Guid duplicateId) return Conflict(new { message = "This resume has already been uploaded to this organization.", resumeId = duplicateId });
        var resume = new Resume { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, CandidateId = candidateId,
            OriginalFileName = Path.GetFileName(file.FileName), ContentType = expectedType, SizeBytes = bytes.Length, Sha256Hash = hash,
            IsPrimary = true, ProcessingStatus = ResumeProcessingStatus.Pending };
        var existingResumes = await db.Resumes.Where(x => x.CandidateId == candidateId && x.IsPrimary).ToListAsync(ct);
        foreach (var old in existingResumes) old.IsPrimary = false;
        string? storageKey = null;
        try
        {
            await using var fileContent = new MemoryStream(bytes, writable: false);
            storageKey = await storage.SaveAsync(tenant.OrganizationId, resume.Id, extension, expectedType, fileContent, ct);
            resume.StorageKey = storageKey;
            db.Resumes.Add(resume);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (storageKey is not null) await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
        return CreatedAtAction(nameof(DownloadResume), new { candidateId, resumeId = resume.Id }, new ResumeDto(resume.Id, resume.OriginalFileName, resume.ContentType, resume.SizeBytes, resume.IsPrimary, resume.ProcessingStatus, resume.CreatedAt));
    }

    [HttpGet("{candidateId:guid}/resumes/{resumeId:guid}")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> DownloadResume(Guid candidateId, Guid resumeId, CancellationToken ct)
    {
        var resume = await db.Resumes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == resumeId && x.CandidateId == candidateId, ct);
        if (resume is null) return NotFound();
        var stream = await storage.OpenReadAsync(resume.StorageKey, ct);
        return File(stream, resume.ContentType, resume.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost("{candidateId:guid}/resumes/{resumeId:guid}/parse")]
    [RequirePermission("candidates.update")]
    public async Task<IActionResult> ParseResume(Guid candidateId, Guid resumeId, CancellationToken ct)
    {
        var resume = await db.Resumes.SingleOrDefaultAsync(x => x.Id == resumeId && x.CandidateId == candidateId, ct);
        if (resume is null) return NotFound();
        var key = $"resume:{resume.Id:N}";
        var processingJob = await db.AIProcessingJobs.SingleOrDefaultAsync(x => x.JobType == "resume_parse" && x.IdempotencyKey == key, ct);
        if (processingJob?.Status is BackgroundJobStatus.Queued or BackgroundJobStatus.Processing)
            return Accepted(new { processingJobId = processingJob.Id, status = processingJob.Status });
        if (processingJob is null)
        {
            processingJob = new AIProcessingJob { OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, JobType = "resume_parse", IdempotencyKey = key, PayloadJson = JsonSerializer.Serialize(new { resumeId }) };
            db.AIProcessingJobs.Add(processingJob);
        }
        else
        {
            processingJob.Status = BackgroundJobStatus.Queued;
            processingJob.ErrorMessage = null;
            processingJob.CompletedAt = null;
        }
        resume.ProcessingStatus = ResumeProcessingStatus.Pending;
        await db.SaveChangesAsync(ct);
        try { backgroundJobs.Enqueue<ResumeParsingBackgroundJob>(job => job.ProcessAsync(tenant.OrganizationId, resume.Id, tenant.UserId, processingJob.Id)); }
        catch
        {
            processingJob.Status = BackgroundJobStatus.Failed;
            processingJob.ErrorMessage = "The resume parsing task could not be queued.";
            resume.ProcessingStatus = ResumeProcessingStatus.Failed;
            await db.SaveChangesAsync(ct);
            return Problem("The resume parsing task could not be queued.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return Accepted(new { processingJobId = processingJob.Id, status = processingJob.Status });
    }

    [HttpGet("{candidateId:guid}/resumes/{resumeId:guid}/parsing")]
    [RequirePermission("candidates.read")]
    public async Task<IActionResult> GetResumeParsing(Guid candidateId, Guid resumeId, CancellationToken ct)
    {
        if (!await db.Resumes.AnyAsync(x => x.Id == resumeId && x.CandidateId == candidateId, ct)) return NotFound();
        var result = await db.ResumeParsingResults.AsNoTracking().Where(x => x.ResumeId == resumeId)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.Status, x.ParserVersion, x.ErrorCode, x.ErrorMessage, x.ProcessedAt, x.ParsedDataJson })
            .FirstOrDefaultAsync(ct);
        var job = await db.AIProcessingJobs.AsNoTracking().Where(x => x.JobType == "resume_parse" && x.IdempotencyKey == $"resume:{resumeId:N}")
            .Select(x => new { x.Id, x.Status, x.AttemptCount, x.ErrorMessage }).FirstOrDefaultAsync(ct);
        if (result is null) return job is null ? NotFound() : Ok(new { processingJob = job, status = job.Status, parsedData = (object?)null });
        using var parsedData = JsonDocument.Parse(result.ParsedDataJson);
        return Ok(new { result.Id, status = job is null ? (object)result.Status : job.Status, result.ParserVersion, result.ErrorCode, result.ErrorMessage, result.ProcessedAt, processingJob = job, parsedData = parsedData.RootElement.Clone() });
    }

    [HttpDelete("{candidateId:guid}/resumes/{resumeId:guid}")]
    [RequirePermission("candidates.update")]
    public async Task<IActionResult> DeleteResume(Guid candidateId, Guid resumeId, CancellationToken ct)
    {
        var resume = await db.Resumes.SingleOrDefaultAsync(x => x.Id == resumeId && x.CandidateId == candidateId, ct);
        if (resume is null) return NotFound();
        var key = resume.StorageKey;
        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(key, ct);
        return NoContent();
    }

    private async Task ReplaceSkillsAsync(Candidate candidate, IReadOnlyCollection<CandidateSkillInput> inputs, CancellationToken ct)
    {
        var old = await db.CandidateSkills.Where(x => x.CandidateId == candidate.Id).ToListAsync(ct);
        db.CandidateSkills.RemoveRange(old);
        var unique = inputs.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
        if (unique.Length == 0) return;
        var names = unique.Select(x => x.Name.Trim()).ToArray(); var normalized = names.Select(x => x.ToUpperInvariant()).ToArray();
        var skills = await db.Skills.Where(x => normalized.Contains(x.NormalizedName)).ToListAsync(ct);
        var known = skills.Select(x => x.NormalizedName).ToHashSet(StringComparer.Ordinal);
        var added = names.Where(x => !known.Contains(x.ToUpperInvariant())).Select(x => new Skill { Name = x, NormalizedName = x.ToUpperInvariant() }).ToArray();
        db.Skills.AddRange(added); skills.AddRange(added);
        foreach (var input in unique)
        {
            var skill = skills.Single(x => x.NormalizedName == input.Name.Trim().ToUpperInvariant());
            db.CandidateSkills.Add(new CandidateSkill { OrganizationId = tenant.OrganizationId, CandidateId = candidate.Id, SkillId = skill.Id, YearsExperience = input.YearsExperience });
        }
    }

    private void AddEducation(Candidate candidate, IReadOnlyCollection<EducationInput> items)
    {
        foreach (var item in items) db.CandidateEducation.Add(new CandidateEducation { OrganizationId = tenant.OrganizationId, CandidateId = candidate.Id,
            Institution = item.Institution.Trim(), Degree = Clean(item.Degree), FieldOfStudy = Clean(item.FieldOfStudy), StartYear = item.StartYear, EndYear = item.EndYear });
    }

    private void AddExperience(Candidate candidate, IReadOnlyCollection<ExperienceInput> items)
    {
        foreach (var item in items) db.CandidateExperience.Add(new CandidateExperience { OrganizationId = tenant.OrganizationId, CandidateId = candidate.Id,
            Company = item.Company.Trim(), JobTitle = Clean(item.JobTitle), Description = Clean(item.Description), StartDate = item.StartDate, EndDate = item.EndDate, IsCurrent = item.IsCurrent });
    }

    private static string? ValidateCandidate(string? first, string? last, string? email, decimal? experience,
        IReadOnlyCollection<CandidateSkillInput> skills, IReadOnlyCollection<EducationInput> education, IReadOnlyCollection<ExperienceInput> jobs)
    {
        if (skills is null || education is null || jobs is null) return "Skills, education, and experience lists are required.";
        if (first?.Trim().Length > 100 || last?.Trim().Length > 100) return "Names are limited to 100 characters.";
        if (!string.IsNullOrWhiteSpace(email) && (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 320)) return "Email address is invalid.";
        if (experience is < 0 or > 80) return "Experience must be between zero and eighty years.";
        if (skills.Count > 100 || skills.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 100 || x.YearsExperience is < 0 or > 80)) return "Candidate skills are invalid.";
        if (education.Count > 30 || education.Any(x => string.IsNullOrWhiteSpace(x.Institution) || x.Institution.Trim().Length > 250 || x.StartYear is < 1900 or > 2200 || x.EndYear is < 1900 or > 2200)) return "Education entries are invalid.";
        if (jobs.Count > 50 || jobs.Any(x => string.IsNullOrWhiteSpace(x.Company) || x.Company.Trim().Length > 250 || x.JobTitle?.Length > 200 || x.Description?.Length > 5000 || (x.IsCurrent && x.EndDate is not null))) return "Experience entries are invalid.";
        if (skills.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != skills.Count) return "Candidate skill names must be unique.";
        return null;
    }

    private static bool HasValidSignature(byte[] bytes, string extension)
    {
        if (extension == ".pdf") return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        if (extension != ".docx" || bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4b || bytes[2] != 0x03 || bytes[3] != 0x04) return false;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return archive.Entries.Count <= 5000 && archive.Entries.Any(x => x.FullName == "word/document.xml") && archive.Entries.Sum(x => x.Length) <= 50L * 1024 * 1024;
        }
        catch (InvalidDataException) { return false; }
    }

    private static string? NormalizeEmail(string? email) => Clean(email)?.ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CandidateSearchRequest(string? Search, string? Location, string? Skill, decimal? MinimumExperience, decimal? MaximumExperience, int Page = 1, int PageSize = 20);
public sealed record CandidateSkillInput(string Name, decimal? YearsExperience);
public sealed record EducationInput(string Institution, string? Degree, string? FieldOfStudy, int? StartYear, int? EndYear);
public sealed record ExperienceInput(string Company, string? JobTitle, string? Description, DateOnly? StartDate, DateOnly? EndDate, bool IsCurrent);
public sealed record CreateCandidateRequest(string? FirstName, string? LastName, string? Email, string? PhoneNumber, string? Location, string? ProfessionalSummary, decimal? TotalExperienceYears,
    IReadOnlyCollection<CandidateSkillInput> Skills, IReadOnlyCollection<EducationInput> Education, IReadOnlyCollection<ExperienceInput> Experience);
public sealed record UpdateCandidateRequest(string? FirstName, string? LastName, string? Email, string? PhoneNumber, string? Location, string? ProfessionalSummary, decimal? TotalExperienceYears,
    IReadOnlyCollection<CandidateSkillInput> Skills, IReadOnlyCollection<EducationInput> Education, IReadOnlyCollection<ExperienceInput> Experience);
public sealed record CandidatePagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);
public sealed record CandidateListItem(Guid Id, string? FirstName, string? LastName, string? Email, string? PhoneNumber, string? Location, decimal? TotalExperienceYears, Guid? PrimaryResumeId);
public sealed record CandidateSkillDto(Guid Id, string Name, decimal? YearsExperience);
public sealed record EducationDto(Guid Id, string Institution, string? Degree, string? FieldOfStudy, int? StartYear, int? EndYear);
public sealed record ExperienceDto(Guid Id, string Company, string? JobTitle, string? Description, DateOnly? StartDate, DateOnly? EndDate, bool IsCurrent);
public sealed record ResumeDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes, bool IsPrimary, ResumeProcessingStatus ProcessingStatus, DateTimeOffset CreatedAt);






