# Deployment

Local Compose runs PostgreSQL 17, ASP.NET Core API, the React production build served by Nginx, and FastAPI. For hot reload, run `npm run dev` from `frontend` outside Compose. Copy `.env.example` to `.env` and set a random `JWT_SIGNING_KEY` (at least 32 bytes) and a local-only PostgreSQL password. PostgreSQL and private local resumes are persisted in named volumes.

For local startup, run `docker compose up --build`; stop with `docker compose down`. Add `-v` only when intentionally deleting local data. The GitHub Actions workflow runs backend tests, frontend build/lint, Python parser tests, and builds all three images. It does not publish images or deploy them; configure those steps in the deployment environment after adding registry and hosting secrets.

For production, set `ASPNETCORE_ENVIRONMENT=Production`, configure `Frontend__Origin` for the deployed frontend origin, provide secrets through the platform secret store, expose the API only through an HTTPS reverse proxy, and apply reviewed EF migrations as a release step before starting API workers. Hangfire.PostgreSql creates its storage schema in PostgreSQL; protect database access and keep the Hangfire dashboard disabled unless separately secured. Configure health probes against `/api/health` and `/api/health/database` and monitor Hangfire failed jobs.

## Resume storage

Docker development stores resumes in the private `resume_data` volume mounted only into the API container. For deployments using S3-compatible object storage, configure `Storage:S3:Bucket` (`S3_BUCKET` in Compose), region, optional service URL, and credentials through a secret manager or workload identity. Provision the bucket with private access and default encryption before starting the API. The API only returns files through a tenant-authorized route.

