# API documentation

## Health

- `GET /api/health` — anonymous process health.
- `GET /api/health/database` — anonymous PostgreSQL connectivity status.

## Authentication

- `POST /api/auth/register` — creates an organization, organization admin, and default organization roles in a transaction.
- `POST /api/auth/login` — accepts organization slug, email, and password.
- `GET /api/auth/me` — returns the authenticated user's tenant-scoped profile and roles.
- `POST /api/auth/refresh` — rotates the refresh token from the HttpOnly cookie; returns `204 No Content` when no session cookie is present and `401` for an invalid or expired cookie.
- `POST /api/auth/logout` — revokes the current refresh token and clears the cookie.
- `POST /api/auth/forgot-password` — accepts organization slug and email; always returns a generic accepted response for unknown accounts.
- `POST /api/auth/reset-password` — consumes a one-time reset token and replaces the password.

The login/register/refresh response contains an access JWT and user profile only. The refresh token is stored as a SHA-256 hash in PostgreSQL and delivered through `hireflow_refresh` with `HttpOnly`, `SameSite=Strict`, `/api/auth` path, and `Secure` outside development. Access tokens expire after 15 minutes by default; refresh tokens after 30 days.

JWT signing configuration is supplied through `Jwt__SigningKey`, `Jwt__Issuer`, and `Jwt__Audience`. Use a secret manager for deployment; `.env.example` contains only local-development values. Password reset delivery uses SMTP when configured. During development without SMTP, the reset link is written to the API log for local testing only; production must configure `Smtp__Host`, `Smtp__From`, and credentials.

## Authorization

ASP.NET Core validates issuer, audience, signature, and lifetime. Tokens include `sub`, `org`, `role`, and permission claims. `[Authorize(Roles = "...")]` enforces role claims; `[RequirePermission("jobs.read")]` resolves a dynamic permission policy. Permission claims are minted from the user's current role grants at login/refresh, so role changes take effect on the next refresh/login.

The API fallback policy requires authentication. Login, registration, refresh, logout, and password reset routes are explicitly anonymous. Tenant-owned EF entities have global query filters driven by the authenticated `org` claim; public login/reset lookups explicitly bypass the filter but constrain by organization slug and normalized email or a token hash.

## Organization management (OrganizationAdmin)

All routes below require an authenticated user with the `OrganizationAdmin` role. The organization comes from the signed `org` claim; request bodies cannot choose a tenant.

- `GET /api/management/organization` — read the current organization profile.
- `PUT /api/management/organization` — update the organization name.
- `GET /api/management/users` — list users and their assigned roles.
- `POST /api/management/users` — create a user with an initial password and one organization role.
- `PUT /api/management/users/{userId}` — update a user name, active status, and role. Self-deactivation and self-demotion are rejected; role/active changes revoke refresh sessions.
- `GET /api/management/roles` — list system/custom roles and granted permission codes.
- `POST /api/management/roles` — create a custom role using known permission codes.
- `PUT /api/management/roles/{roleId}` — update a custom role. Built-in roles are immutable.
- `DELETE /api/management/roles/{roleId}` — delete an unused custom role.
- `GET /api/management/permissions` — list the seeded permission catalog.
- `GET /api/management/departments`, `POST /api/management/departments` — list and create departments, with tenant-scoped parent validation.

The permission catalog is seeded idempotently when the API starts after database migrations. New organization admins receive all catalog permissions. Custom roles can receive selected permissions. Existing access JWTs keep their minted claims until expiry; refresh tokens are revoked for affected accounts.
## Jobs

Authenticated job management uses the tenant from the `org` claim and requires the matching job permission.

- `GET /api/jobs` — paged tenant job search. Supports `Search`, `DepartmentId`, `MinimumExperience`, `MaximumExperience`, `Status`, `Page`, `PageSize`, `SortBy` (`title`, `status`, `department`, or created date), and `Descending`.
- `GET /api/jobs/{jobId}` — job details with skills and assigned recruiters.
- `POST /api/jobs` — create a job, validate department/recruiters, set status, and add skills.
- `PUT /api/jobs/{jobId}` — update a job and replace its skills/recruiter assignments.
- `DELETE /api/jobs/{jobId}` — archive by setting status to Closed.
- `GET /api/jobs/departments` — return organization-scoped department choices for job creators; initializes standard choices when an organization has none.
- `GET /api/jobs/public` — anonymous paged search of open jobs across active organizations. Supports `search`, `organization`, `page`, and `pageSize`.
- `GET /api/jobs/public/{jobId}` — anonymous details for an open job.

Recruiters receive job and candidate workflow permissions by default; interviewers receive interview read and feedback permissions. The public React job board uses anonymous listing; recruiter sign-in opens the tenant job workspace.

## Candidates and resumes

Candidate routes require an authenticated user with the relevant `candidates.*` permission. All profile, skill, education, experience, and resume queries are scoped to the authenticated organization.

- `GET /api/candidates` — paged search by name/email, location, skill, and experience range.
- `GET /api/candidates/{candidateId}` — candidate profile, skills, education, experience, and resume metadata.
- `POST /api/candidates` — create a candidate with nested skills, education, and work history. Duplicate normalized email within the organization returns `409` with the existing candidate ID.
- `PUT /api/candidates/{candidateId}` — update profile and replace supplied skills, education, and work history.
- `POST /api/candidates/{candidateId}/resumes` — multipart upload named `file`; accepts PDF/DOCX up to 10 MB, checks MIME type and file signature, hashes content for organization-level duplicate detection, and keeps at most five resumes per candidate.
- `GET /api/candidates/{candidateId}/resumes/{resumeId}` — authorized resume download; no public URL is issued.
- `DELETE /api/candidates/{candidateId}/resumes/{resumeId}` — delete resume metadata and private object.

Resume data is stored outside PostgreSQL. Docker development uses a persistent private volume. Set `Storage:S3:Bucket` to use the S3 adapter; configure `Storage:S3:Region`, optional `Storage:S3:ServiceUrl`, credentials or workload identity, and path-style addressing when required. S3 uploads set MIME type and request AES-256 server-side encryption. Create the bucket as private before deploying.

- `POST /api/candidates/{candidateId}/resumes/{resumeId}/parse` — enqueue idempotent resume parsing and return `202 Accepted` with the processing job ID.
- `GET /api/candidates/{candidateId}/resumes/{resumeId}/parsing` — retrieve queued/processing/completed/failed job state and, when available, the latest extraction result and parser warnings.
- `GET /api/applications` — return a paged organization-scoped application board and active recruitment stages.
- `POST /api/applications` — create an application for an existing candidate and open job; duplicate applications are rejected.
- `PUT /api/applications/{applicationId}/stage` — move an application through valid pipeline transitions and append immutable stage history.
- `GET /api/applications/{applicationId}/history` — read the application stage audit trail.
- `GET /api/interviews` — list organization interviews with candidate, job, interviewers, and feedback counts.
- `POST /api/interviews` — schedule a UTC interview round for an application and active organization users, rejecting interviewer conflicts.
- `PUT /api/interviews/{interviewId}/schedule` — reschedule an interview, check interviewer conflicts, and notify assigned interviewers.
- `POST /api/interviews/{interviewId}/feedback` — submit one rating and recommendation per assigned interviewer.
- `GET /api/interviews/{interviewId}/feedback` — view interview feedback for authorized organization users.
- `/hubs/notifications` — authenticated SignalR endpoint; each connection joins only its organization and user notification group.
- `GET /api/notifications` — return the current user's paged notification history and unread count.
- `PUT /api/notifications/{notificationId}/read` — mark one of the current user's notifications as read.
- `GET /api/analytics/dashboard` — return tenant-scoped KPI, funnel, monthly trend, job, and recruiter metrics.
- `GET /api/analytics/applications.csv` — export organization application data as CSV.
- `POST /api/assistant/query` — run one of the validated candidate search, candidate summary, saved-match explanation, or job-description template tasks. No SQL is generated from user input.

The parser service is reachable to the .NET API through `AiService:BaseUrl`; clients should call the .NET routes only. The AI service has no database credentials and cannot persist application data itself.

## Candidate matching

- `POST /api/jobs/{jobId}/matches/{candidateId}` — calculate and persist a versioned candidate-to-job score. Requires `jobs.read` and `candidates.read` in the authenticated tenant.
- `GET /api/jobs/{jobId}/matches?page=1&pageSize=20` — return each candidate's latest match, ordered by score, with paging, matched/missing skills, category scores, and warnings.

The current deterministic algorithm version is `deterministic-1.0`. Default weights are required skills 50%, experience 25%, project relevance 15%, and education 10%. Weights are configurable in `Matching:Weights`; values must be nonnegative and sum to 1, otherwise defaults are used. Scores support recruiter review and never automatically reject a candidate.

- `POST /api/jobs/public/{jobId}/applications` — submit a public application with name and contact information; duplicate submissions are rejected and requests are rate limited.
