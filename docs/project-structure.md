# Phase 1 structure

```text
backend/
  HireFlow.sln
  global.json
  Dockerfile
  src/
    HireFlow.Domain/{Common,Entities,Enums,Events}
    HireFlow.Application/{Abstractions,DTOs,Features,Validation}
    HireFlow.Infrastructure/{Persistence,Storage,Authentication,BackgroundJobs}
    HireFlow.API/{Controllers,Middleware,Authorization}
  tests/{HireFlow.UnitTests,HireFlow.IntegrationTests}
frontend/
  src/{app,components,features,hooks,services,theme,types}
ai-service/
  app/{api,core,schemas,services}
  tests/
docs/
```

These are module boundaries only at this stage. Implementation follows the requested phase order; Phase 2 will introduce persistence entities and migrations.
