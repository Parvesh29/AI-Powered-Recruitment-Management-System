using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Jobs;
using HireFlow.Domain.Enums;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HireFlow.API.Notifications;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Recruitment;
using HireFlow.Domain.Entities.Organizations;
using Microsoft.AspNetCore.SignalR;
using RecruitmentApplication = HireFlow.Domain.Entities.Recruitment.Application;
using Microsoft.AspNetCore.RateLimiting;
using System.IO.Compression;
using System.Security.Cryptography;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize]
public sealed class JobsController(HireFlowDbContext db, ICurrentTenant tenant, IHubContext<NotificationsHub> hub, IResumeFileStorage resumeStorage) : ControllerBase
{
    private const long MaximumResumeBytes = 10 * 1024 * 1024;
    [HttpGet("departments")]
    [RequirePermission("jobs.read")]
    public async Task<IActionResult> GetJobDepartments(CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(ct))
        {
            await AddDefaultDepartmentsAsync(ct);
        }
        else
        {
            var current = await db.Departments.ToListAsync(ct);
            var previousDefaults = new[] { "Engineering", "Product", "Design", "Sales", "Operations" };
            if (current.Count == previousDefaults.Length && current.Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(previousDefaults))
            {
                var replacements = new[] { "Computer Science", "Information Technology (IT)", "Mechanical Engineering", "Electrical Engineering", "Civil Engineering" };
                for (var index = 0; index < current.Count; index++) current[index].Name = replacements[index];
                db.Departments.AddRange(new[] { "Electronics & Communication", "Human Resources", "Finance" }
                    .Select(name => new Department { OrganizationId = tenant.OrganizationId, Name = name }));
                await db.SaveChangesAsync(ct);
            }
        }
        return Ok(await db.Departments.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.ParentDepartmentId }).ToListAsync(ct));
    }

    private async Task AddDefaultDepartmentsAsync(CancellationToken ct)
    {
        var defaults = new[]
        {
            "Computer Science", "Information Technology (IT)", "Mechanical Engineering", "Electrical Engineering",
            "Civil Engineering", "Electronics & Communication", "Human Resources", "Finance"
        };
        db.Departments.AddRange(defaults.Select(name => new Department { OrganizationId = tenant.OrganizationId, Name = name }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<Department>().Where(x => x.State == EntityState.Added)) entry.State = EntityState.Detached;
        }
    }

    [HttpGet]
    [RequirePermission("jobs.read")]
    public async Task<IActionResult> Search([FromQuery] JobSearchRequest request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100) return BadRequest("Page must be positive and page size must be between 1 and 100.");
        var query = db.Jobs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.Description.Contains(term) || (x.Location != null && x.Location.Contains(term)));
        }
        if (request.DepartmentId is Guid departmentId) query = query.Where(x => x.DepartmentId == departmentId || db.JobDepartments.Any(jd => jd.JobId == x.Id && jd.DepartmentId == departmentId));
        if (request.Status is JobStatus status) query = query.Where(x => x.Status == status);
        if (request.MinimumExperience is int min) query = query.Where(x => x.MaximumExperienceYears == null || x.MaximumExperienceYears >= min);
        if (request.MaximumExperience is int max) query = query.Where(x => x.MinimumExperienceYears <= max);
        query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch
        {
            ("title", false) => query.OrderBy(x => x.Title),
            ("title", true) => query.OrderByDescending(x => x.Title),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("department", false) => query.OrderBy(x => x.Department!.Name),
            ("department", true) => query.OrderByDescending(x => x.Department!.Name),
            (_, true) => query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderBy(x => x.CreatedAt)
        };
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new JobListItem(x.Id, x.Title, x.Location, x.EmploymentType, x.MinimumExperienceYears, x.MaximumExperienceYears,
                x.Status, x.DepartmentId, x.Department!.Name,
                db.JobDepartments.Where(jd => jd.JobId == x.Id).Select(jd => jd.DepartmentId).ToList(),
                db.JobDepartments.Where(jd => jd.JobId == x.Id).Join(db.Departments, jd => jd.DepartmentId, d => d.Id, (jd, d) => d.Name).OrderBy(name => name).ToList(),
                x.CreatedAt, x.PublishedAt))
            .ToListAsync(ct);
        return Ok(new PagedResult<JobListItem>(items, total, request.Page, request.PageSize));
    }

    [HttpGet("{jobId:guid}")]
    [RequirePermission("jobs.read")]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().Where(x => x.Id == jobId).Select(x => new
        {
            x.Id, x.Title, x.Description, x.Location, x.EmploymentType, x.MinimumExperienceYears, x.MaximumExperienceYears,
            x.Status, x.DepartmentId, DepartmentName = x.Department!.Name, x.CreatedAt, x.UpdatedAt, x.PublishedAt,
            Departments = db.JobDepartments.Where(jd => jd.JobId == x.Id).Join(db.Departments, jd => jd.DepartmentId, d => d.Id, (jd, d) => new { d.Id, d.Name }).OrderBy(d => d.Name).ToList(),
            Skills = db.JobSkills.Where(s => s.JobId == x.Id).Join(db.Skills, s => s.SkillId, skill => skill.Id, (s, skill) => new JobSkillDto(skill.Id, skill.Name, s.IsRequired)).ToList(),
            Recruiters = db.JobRecruiters.Where(r => r.JobId == x.Id).Join(db.Users, r => r.RecruiterUserId, user => user.Id, (r, user) => new JobRecruiterDto(user.Id, user.FirstName, user.LastName, user.Email)).ToList()
        }).SingleOrDefaultAsync(ct);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    [RequirePermission("jobs.create")]
    public async Task<IActionResult> Create(CreateJobRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest("Status is invalid.");
        var departmentIds = NormalizeDepartmentIds(request.DepartmentId, request.DepartmentIds);
        var validation = await ValidateJobAsync(request.Title, request.Description, request.MinimumExperienceYears, request.MaximumExperienceYears,
            departmentIds, request.SkillNames, request.RecruiterIds, ct);
        if (validation is not null) return BadRequest(validation);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            OrganizationId = tenant.OrganizationId, CreatedBy = tenant.UserId, DepartmentId = departmentIds.First(),
            Title = request.Title.Trim(), Description = request.Description.Trim(), Location = Clean(request.Location),
            EmploymentType = Clean(request.EmploymentType), MinimumExperienceYears = request.MinimumExperienceYears,
            MaximumExperienceYears = request.MaximumExperienceYears, Status = request.Status,
            PublishedAt = request.Status == JobStatus.Open ? now : null, ClosedAt = request.Status == JobStatus.Closed ? now : null
        };
        db.Jobs.Add(job);
        await ReplaceDepartmentsAsync(job, departmentIds, ct);
        await ReplaceSkillsAsync(job, request.SkillNames, ct);
        await ReplaceRecruitersAsync(job, request.RecruiterIds, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return CreatedAtAction(nameof(Get), new { jobId = job.Id }, new { job.Id, job.Title, job.Status, job.DepartmentId });
    }

    [HttpPut("{jobId:guid}")]
    [RequirePermission("jobs.update")]
    public async Task<IActionResult> Update(Guid jobId, UpdateJobRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest("Status is invalid.");
        var departmentIds = NormalizeDepartmentIds(request.DepartmentId, request.DepartmentIds);
        var validation = await ValidateJobAsync(request.Title, request.Description, request.MinimumExperienceYears, request.MaximumExperienceYears,
            departmentIds, request.SkillNames, request.RecruiterIds, ct);
        if (validation is not null) return BadRequest(validation);
        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null) return NotFound();
        var oldStatus = job.Status;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        job.DepartmentId = departmentIds.First();
        job.Title = request.Title.Trim();
        job.Description = request.Description.Trim();
        job.Location = Clean(request.Location);
        job.EmploymentType = Clean(request.EmploymentType);
        job.MinimumExperienceYears = request.MinimumExperienceYears;
        job.MaximumExperienceYears = request.MaximumExperienceYears;
        job.Status = request.Status;
        if (oldStatus != JobStatus.Open && request.Status == JobStatus.Open) job.PublishedAt = DateTimeOffset.UtcNow;
        job.ClosedAt = request.Status == JobStatus.Closed ? job.ClosedAt ?? DateTimeOffset.UtcNow : null;
        await ReplaceDepartmentsAsync(job, departmentIds, ct);
        await ReplaceSkillsAsync(job, request.SkillNames, ct);
        await ReplaceRecruitersAsync(job, request.RecruiterIds, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { job.Id, job.Title, job.Status, job.DepartmentId, job.UpdatedAt });
    }

    [HttpDelete("{jobId:guid}")]
    [RequirePermission("jobs.delete")]
    public async Task<IActionResult> Archive(Guid jobId, CancellationToken ct)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null) return NotFound();
        job.Status = JobStatus.Closed;
        job.ClosedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicSearch([FromQuery] string? search, [FromQuery] string? organization,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("Page must be positive and page size must be between 1 and 100.");
        var query = from job in db.Jobs.IgnoreQueryFilters().AsNoTracking()
                    join org in db.Organizations.AsNoTracking() on job.OrganizationId equals org.Id
                    where job.Status == JobStatus.Open && org.IsActive
                    select new { job, org };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.job.Title.Contains(term) || x.job.Description.Contains(term) || (x.job.Location != null && x.job.Location.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(organization)) query = query.Where(x => x.org.Slug == organization.Trim().ToLowerInvariant());
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.job.PublishedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new PublicJobItem(x.job.Id, x.org.Name, x.org.Slug, x.job.Title, x.job.Description, x.job.Location,
                x.job.EmploymentType, x.job.MinimumExperienceYears, x.job.MaximumExperienceYears, x.job.PublishedAt))
            .ToListAsync(ct);
        return Ok(new PagedResult<PublicJobItem>(items, total, page, pageSize));
    }

    [HttpGet("public/{jobId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicGet(Guid jobId, CancellationToken ct)
    {
        var job = await (from item in db.Jobs.IgnoreQueryFilters().AsNoTracking()
                         join org in db.Organizations.AsNoTracking() on item.OrganizationId equals org.Id
                         where item.Id == jobId && item.Status == JobStatus.Open && org.IsActive
                         select new PublicJobItem(item.Id, org.Name, org.Slug, item.Title, item.Description, item.Location,
                             item.EmploymentType, item.MinimumExperienceYears, item.MaximumExperienceYears, item.PublishedAt))
            .SingleOrDefaultAsync(ct);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost("public/{jobId:guid}/applications")]
    [AllowAnonymous]
    [EnableRateLimiting("public-applications")]
    [RequestSizeLimit(MaximumResumeBytes + 1024 * 1024)]
    public async Task<IActionResult> PublicApply(Guid jobId, [FromForm] PublicApplicationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 100 || request.LastName?.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(request.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email) || request.Email.Length > 320 || request.PhoneNumber?.Length > 40)
            return BadRequest("Name, valid email, and an optional phone number are required.");
        if (request.Resume is null) return BadRequest("A resume is required to submit an application.");
        string? extension = null;
        string? contentType = null;
        byte[]? resumeBytes = null;
        if (request.Resume is { } file)
        {
            if (file.Length is <= 0 or > MaximumResumeBytes) return BadRequest("Choose a PDF or DOCX resume no larger than 10 MB.");
            extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            contentType = extension switch { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", _ => null };
            if (contentType is null || !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase)) return BadRequest("Only PDF and DOCX files with a matching content type are accepted.");
            await using var memory = new MemoryStream((int)file.Length);
            await file.CopyToAsync(memory, ct);
            resumeBytes = memory.ToArray();
            if (!HasValidResumeSignature(resumeBytes, extension)) return BadRequest("The uploaded file signature does not match its file type.");
        }
        var job = await db.Jobs.IgnoreQueryFilters().Where(x => x.Id == jobId && x.Status == JobStatus.Open)
            .Select(x => new { x.Id, x.OrganizationId, x.Title }).SingleOrDefaultAsync(ct);
        if (job is null) return NotFound("Open job was not found.");
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var existingCandidate = await db.Candidates.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == job.OrganizationId && x.NormalizedEmail == normalizedEmail, ct);
        if (existingCandidate is not null && await db.Applications.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == job.OrganizationId && x.CandidateId == existingCandidate.Id && x.JobId == jobId, ct))
            return Conflict("An application from this email already exists for this job.");

        var stage = await db.RecruitmentStages.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == job.OrganizationId && x.Name == "Applied", ct);
        if (stage is null)
        {
            stage = new RecruitmentStage { OrganizationId = job.OrganizationId, Name = "Applied", SortOrder = 0 };
            db.RecruitmentStages.Add(stage);
        }
        var candidate = existingCandidate ?? new Candidate { OrganizationId = job.OrganizationId, FirstName = request.FirstName.Trim(), LastName = request.LastName?.Trim(), Email = request.Email.Trim().ToLowerInvariant(), NormalizedEmail = normalizedEmail, PhoneNumber = request.PhoneNumber?.Trim() };
        if (existingCandidate is null) db.Candidates.Add(candidate);
        Resume? resume = null;
        string? storageKey = null;
        if (resumeBytes is not null && extension is not null && contentType is not null)
        {
            var hash = Convert.ToHexString(SHA256.HashData(resumeBytes));
            if (await db.Resumes.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == job.OrganizationId && x.Sha256Hash == hash, ct)) return Conflict("This resume has already been uploaded to this organization.");
            var existingResumes = await db.Resumes.IgnoreQueryFilters().Where(x => x.OrganizationId == job.OrganizationId && x.CandidateId == candidate.Id && x.IsPrimary).ToListAsync(ct);
            foreach (var old in existingResumes) old.IsPrimary = false;
            resume = new Resume { OrganizationId = job.OrganizationId, CandidateId = candidate.Id, OriginalFileName = Path.GetFileName(request.Resume!.FileName), ContentType = contentType, SizeBytes = resumeBytes.Length, Sha256Hash = hash, IsPrimary = true, ProcessingStatus = ResumeProcessingStatus.Pending };
            try
            {
                await using var content = new MemoryStream(resumeBytes, writable: false);
                storageKey = await resumeStorage.SaveAsync(job.OrganizationId, resume.Id, extension, contentType, content, ct);
                resume.StorageKey = storageKey;
                db.Resumes.Add(resume);
            }
            catch
            {
                if (storageKey is not null) await resumeStorage.DeleteAsync(storageKey, CancellationToken.None);
                throw;
            }
        }
        var application = new RecruitmentApplication { OrganizationId = job.OrganizationId, CandidateId = candidate.Id, JobId = jobId, CurrentStageId = stage.Id, Source = CandidateSource.CareersPage };
        db.Applications.Add(application);
        db.ApplicationStageHistory.Add(new ApplicationStageHistory { OrganizationId = job.OrganizationId, ApplicationId = application.Id, ToStageId = stage.Id, Notes = "Public application submitted." });
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            if (storageKey is not null) await resumeStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
        var recruiterIds = await db.JobRecruiters.IgnoreQueryFilters().Where(x => x.OrganizationId == job.OrganizationId && x.JobId == jobId).Select(x => x.RecruiterUserId).ToListAsync(ct);
        foreach (var recruiterId in recruiterIds)
            await NotificationDispatcher.SendAsync(db, hub, job.OrganizationId, recruiterId, "application_received", "New application", $"A candidate applied for {job.Title}.", new { applicationId = application.Id, jobId }, ct);
        return Accepted(new { application.Id, message = "Your application was submitted." });
    }

    private static bool HasValidResumeSignature(byte[] bytes, string extension)
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

    private async Task<string?> ValidateJobAsync(string title, string description, int minimum, int? maximum, IReadOnlyCollection<Guid> departmentIds,
        IReadOnlyCollection<string> skillNames, IReadOnlyCollection<Guid> recruiterIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) return "Title is required and limited to 200 characters.";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 20000) return "Description is required and limited to 20,000 characters.";
        if (minimum < 0 || maximum < 0 || maximum is int max && max < minimum) return "Experience range is invalid.";
        if (departmentIds.Count is < 1 or > 25 || departmentIds.Distinct().Count() != departmentIds.Count) return "Select between one and twenty-five unique departments.";
        if (await db.Departments.CountAsync(x => departmentIds.Contains(x.Id), ct) != departmentIds.Count) return "One or more departments do not exist in this organization.";
        if (skillNames.Count > 50 || skillNames.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 100)) return "Provide up to 50 valid skill names.";
        if (skillNames.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != skillNames.Count) return "Skill names must be unique.";
        if (recruiterIds.Distinct().Count() != recruiterIds.Count || recruiterIds.Count > 25) return "Recruiter IDs must be unique and limited to 25.";
        if (recruiterIds.Count > 0)
        {
            var eligible = await (from user in db.Users
                                  join assignment in db.UserRoles on new { user.OrganizationId, UserId = user.Id } equals new { assignment.OrganizationId, assignment.UserId }
                                  join role in db.Roles on new { assignment.OrganizationId, RoleId = assignment.RoleId } equals new { role.OrganizationId, RoleId = role.Id }
                                  where recruiterIds.Contains(user.Id) && user.IsActive && (role.Name == "Recruiter" || role.Name == "OrganizationAdmin")
                                  select user.Id).Distinct().CountAsync(ct);
            if (eligible != recruiterIds.Count) return "One or more recruiter IDs are invalid for this organization.";
        }
        return null;
    }

    private async Task ReplaceSkillsAsync(Job job, IReadOnlyCollection<string> names, CancellationToken ct)
    {
        var existing = await db.JobSkills.Where(x => x.JobId == job.Id).ToListAsync(ct);
        db.JobSkills.RemoveRange(existing);
        var clean = names.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (clean.Length == 0) return;
        var normalized = clean.Select(x => x.ToUpperInvariant()).ToArray();
        var skills = await db.Skills.Where(x => normalized.Contains(x.NormalizedName)).ToListAsync(ct);
        var known = skills.Select(x => x.NormalizedName).ToHashSet(StringComparer.Ordinal);
        var newSkills = clean.Where(x => !known.Contains(x.ToUpperInvariant())).Select(x => new Skill { Name = x, NormalizedName = x.ToUpperInvariant() }).ToArray();
        db.Skills.AddRange(newSkills);
        skills.AddRange(newSkills);
        foreach (var skill in skills)
            db.JobSkills.Add(new JobSkill { OrganizationId = tenant.OrganizationId, JobId = job.Id, SkillId = skill.Id, IsRequired = true });
    }

    private async Task ReplaceRecruitersAsync(Job job, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var existing = await db.JobRecruiters.Where(x => x.JobId == job.Id).ToListAsync(ct);
        db.JobRecruiters.RemoveRange(existing);
        foreach (var userId in userIds.Distinct())
            db.JobRecruiters.Add(new JobRecruiter { OrganizationId = tenant.OrganizationId, JobId = job.Id, RecruiterUserId = userId });
    }

    private async Task ReplaceDepartmentsAsync(Job job, IReadOnlyCollection<Guid> departmentIds, CancellationToken ct)
    {
        var existing = await db.JobDepartments.Where(x => x.JobId == job.Id).ToListAsync(ct);
        db.JobDepartments.RemoveRange(existing);
        foreach (var departmentId in departmentIds)
            db.JobDepartments.Add(new JobDepartment { OrganizationId = tenant.OrganizationId, JobId = job.Id, DepartmentId = departmentId });
    }

    private static IReadOnlyCollection<Guid> NormalizeDepartmentIds(Guid primaryDepartmentId, IReadOnlyCollection<Guid>? departmentIds)
    {
        var normalized = departmentIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? [];
        if (normalized.Count == 0 && primaryDepartmentId != Guid.Empty) normalized.Add(primaryDepartmentId);
        else if (primaryDepartmentId != Guid.Empty && !normalized.Contains(primaryDepartmentId)) normalized.Insert(0, primaryDepartmentId);
        return normalized;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record JobSearchRequest(string? Search, Guid? DepartmentId, int? MinimumExperience, int? MaximumExperience,
    JobStatus? Status, int Page = 1, int PageSize = 20, string? SortBy = null, bool Descending = true);
public sealed record CreateJobRequest(string Title, string Description, string? Location, string? EmploymentType,
    int MinimumExperienceYears, int? MaximumExperienceYears, Guid DepartmentId, JobStatus Status,
    IReadOnlyCollection<string> SkillNames, IReadOnlyCollection<Guid> RecruiterIds, IReadOnlyCollection<Guid>? DepartmentIds = null);
public sealed record UpdateJobRequest(string Title, string Description, string? Location, string? EmploymentType,
    int MinimumExperienceYears, int? MaximumExperienceYears, Guid DepartmentId, JobStatus Status,
    IReadOnlyCollection<string> SkillNames, IReadOnlyCollection<Guid> RecruiterIds, IReadOnlyCollection<Guid>? DepartmentIds = null);
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);
public sealed record JobListItem(Guid Id, string Title, string? Location, string? EmploymentType, int MinimumExperienceYears,
    int? MaximumExperienceYears, JobStatus Status, Guid DepartmentId, string DepartmentName, IReadOnlyCollection<Guid> DepartmentIds,
    IReadOnlyCollection<string> DepartmentNames, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt);
public sealed record JobSkillDto(Guid Id, string Name, bool IsRequired);
public sealed record JobRecruiterDto(Guid Id, string FirstName, string LastName, string Email);
public sealed record PublicJobItem(Guid Id, string OrganizationName, string OrganizationSlug, string Title, string Description,
    string? Location, string? EmploymentType, int MinimumExperienceYears, int? MaximumExperienceYears, DateTimeOffset? PublishedAt);
public sealed record PublicApplicationRequest(string FirstName, string? LastName, string Email, string? PhoneNumber, IFormFile? Resume = null);


