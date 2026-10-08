# Student Project Management System | Skeleton v0.1

Boilerplate for the student-topic and project-progress management system. This is a **scaffold**, not an implemented product. Functional Requirements FR-01 through FR-21 and UC-01 through UC-43 are mapped into modules with placeholders, following the supplied Week 1 and Week 2 analyses.

## Technology

- Frontend: Vue 3, TypeScript, Vite, Vue Router, Pinia.
- Backend: ASP.NET Core 10 Web API using a 4-project layered solution.
- Persistence draft: EF Core SQL Server (no migration, tables or running database required for skeleton).
- Integrations: contracts only for GitHub, AI analysis, deadline scheduler and notification dispatch.

The supplied Lab Booking frontend/backend repositories were used **only for technology and layout architecture reference**. The supplied `student-project-management-system - Shortcut.zip` contains a Windows `.lnk` shortcut rather than a working repository. No Lab Booking domain code has been copied into this project.

## Run frontend

Requires Node.js 20.19+ or 22.12+.

```bash
cd frontend
npm install
npm run dev
# http://localhost:5173
```

`npm run typecheck` and `npm run build` check the frontend. User role selection in the UI is **preview only**, not authentication or permission enforcement.

## Run backend

Requires .NET 10 SDK. SQL Server is **not required** until the database integration phase.

```bash
cd backend
# Optionally set Jwt__Key with a strong random value (required outside Development).
dotnet restore StudentProjects.sln
DOTNET_ENVIRONMENT=Development dotnet run --project StudentProjects.Api
# http://localhost:5103/health
# http://localhost:5103/openapi/v1.json (Development only)
```

Development-only JWT placeholder is not a production secret. Set `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, and (when implementing data access) `ConnectionStrings__Default` via environment variables / secret manager for deployment.

Every business endpoint intentionally returns HTTP **501 Not Implemented**, with a `module`, `useCases`, and `nextStep` response. Login is not functional, so protected endpoints may instead return 401 or 403 as expected. Only `/health` and OpenAPI description are live scaffold endpoints.

## Structure

```text
frontend/src/
  app/          router, typed module navigation registry
  components/   navigation and reusable module shell
  features/     individual feature API contract/type skeletons
  stores/       local preview role only
  views/        login, dashboard, module skeleton and 404
  styles/       responsive layout CSS
backend/
  StudentProjects.Api/             endpoint mapping and auth boundary
  StudentProjects.Application/     use-case service contracts
  StudentProjects.Domain/          entities and state enums
  StudentProjects.Infrastructure/  draft EF Core context + integration stubs
  StudentProjects.sln
  ...
docs/
  ARCHITECTURE.md
  REQUIREMENTS-MAPPING.md
  ERD-DRAFT.md
```

## Scaffold constraints

- **No** business workflows, fake accounts, fake reports, dashboard metrics, GitHub credentials or AI integration.
- AuthN/AuthZ boundaries are wired at the API level, but tokens and user management are not implemented.
- Lecturer capacity **must** be rechecked at the server within an atomic transaction on acceptance (FR-10). Domain fields and a SQL check constraint are only the starting point.
- AI report content is advisory; lecturer makes the assessment (FR-16).
- No database migrations until the Week 3 ERD and Week 4 schema are approved.
- For mapping and current limitations see `docs/`.
