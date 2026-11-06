# HireFlow AI

Multi-tenant recruitment and applicant tracking platform.

## Current phase

Phases 1–14 are implemented: multi-tenant authentication and organization management, permission-based roles, job and candidate workflows, private resume storage and queued parsing, deterministic candidate matching, recruitment pipeline and stage history, interview scheduling and feedback, SignalR notifications, recruitment analytics, and a structured recruitment assistant.

## Architecture

- `backend/src/HireFlow.Domain` — entities and enums.
- `backend/src/HireFlow.Application` — future use cases, contracts, DTOs and validation.
- `backend/src/HireFlow.Infrastructure` — EF Core persistence, PostgreSQL provider, auth services, and migrations.
- `backend/src/HireFlow.API` — HTTP endpoints, Hangfire worker, SignalR hub, and composition root.
- `frontend` — React and TypeScript client.
- `ai-service` — separate FastAPI process; it has no direct application database access.
- PostgreSQL is accessed by the .NET API. Local development uses Docker Compose.

See [architecture](docs/architecture.md), [database design and ER diagram](docs/database-design.md), and [deployment](docs/deployment.md).

## Technology versions

.NET 10 LTS, EF Core 10.0.14, Npgsql EF provider 10.0.3, FluentValidation 12.2.0, JWT bearer 10.0.14, Hangfire.AspNetCore 1.8.25, Hangfire.PostgreSql 1.21.1, Node.js 24 LTS, React 19, TypeScript 7, Python 3.14, FastAPI 0.162, PostgreSQL 17, and AWS S3 SDK for .NET 4.0.104.8. Package versions are pinned in project manifests and the local `dotnet-ef` tool manifest.

## Run locally

Copy `.env.example` to `.env`, then run `docker compose up --build -d`. Development API startup applies pending migrations. Services: frontend `http://localhost:5173`, API `http://localhost:8080`, API auth `http://localhost:8080/api/auth`, API health `http://localhost:8080/api/health/database`, FastAPI docs `http://localhost:8000/docs`, PostgreSQL `localhost:5432`.

To generate/apply migrations from the repository root:

```powershell
dotnet tool restore
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=hireflow;Username=hireflow;Password=local_dev_only"
dotnet tool run dotnet-ef migrations add MigrationName --project backend/src/HireFlow.Infrastructure --startup-project backend/src/HireFlow.API
dotnet tool run dotnet-ef database update --project backend/src/HireFlow.Infrastructure --startup-project backend/src/HireFlow.API
```

Production deployments should apply reviewed migrations as a deployment step. The frontend container builds static assets and serves them through Nginx. Do not use example local passwords outside development.

For local development without Docker:

- `dotnet run --project backend/src/HireFlow.API` (configure `ConnectionStrings:DefaultConnection` and a 32+ byte `Jwt:SigningKey` first)
- `cd frontend && npm install && npm run dev`
- `cd ai-service && .venv/Scripts/Activate.ps1 && uvicorn app.main:app --reload`

## Verify

```powershell
dotnet build backend/HireFlow.sln
dotnet test backend/HireFlow.sln
```

The xUnit suite includes organization-scoped EF query-filter and deterministic matching tests. Frontend checks use `npm run build` and `npm run lint`; Python parser tests use `python -m pytest` in `ai-service`. GitHub Actions runs backend tests, frontend build/lint, Python tests, and container image builds.
## Scope note

Candidate resumes use private local storage in Docker development and can use an S3-compatible bucket in production. Configure `S3_BUCKET`, region, endpoint if needed, and credentials or AWS workload identity; keep the bucket private and enable encryption. Hangfire job records are stored in PostgreSQL through Hangfire.PostgreSql. The assistant uses bounded, validated deterministic operations and does not require a hosted model. Docker images have been built and the Compose stack started successfully; backend and Python tests pass. Broader integration and frontend component tests remain future coverage. The JWT key in `.env` is for local development only.

## Screenshots

Screenshots can be added here after launching the local stack.







