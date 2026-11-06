# Architecture

HireFlow is a modular monorepo with dependency direction Domain <- Application <- Infrastructure/API. The API is the trusted boundary for authentication, organization resolution, persistence and authorization. The Python service handles AI workloads through internal HTTP calls and has no direct database access.

```text
React SPA -> ASP.NET Core API -> PostgreSQL
                         |----> FastAPI AI service
```

Persistence uses EF Core 10 and Npgsql against the `hireflow` PostgreSQL schema. Migrations are stored in `HireFlow.Infrastructure/Persistence/Migrations`. Development startup applies pending migrations; deployment environments should apply reviewed migrations explicitly.

Organization-owned rows carry `OrganizationId`; composite alternate keys and foreign keys make cross-organization references invalid at the database layer. The API validates JWT issuer, audience, signature, and expiry. The `org` claim supplies a scoped tenant context used by global EF query filters; composite tenant foreign keys additionally reject cross-organization references.

