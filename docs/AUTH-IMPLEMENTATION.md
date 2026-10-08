# Authentication Implementation v0.3

## Phạm vi

- ASP.NET Core 10 + SQL Server + Entity Framework Core 10.
- Users (1 role / user) -> Roles (Student, Lecturer, Admin), role names used as policy claims.
- JWT HS256, expiry 30 minutes; server validates active account, role and token version on EVERY authenticated request.
- POST `/api/v1/auth/login` (public), POST `/api/v1/auth/logout` (authenticated), GET `/api/v1/auth/me` (authenticated).
- Logout invalidates **all access tokens for that account** by incrementing `TokenVersion`. No refresh token is issued. Client keeps access token only in memory and requires re-login on refresh.
- Only Authentication functions are implemented; every other existing placeholder retains 501 (after authenticated/role check).
- Dev Admin only created when `AuthBootstrap:Email` and `AuthBootstrap:Password` user secrets are set, and only during Development startup.

## Windows setup

1. Install .NET 10 SDK and SQL Server Express LocalDB. Ensure `sqllocaldb info` lists `MSSQLLocalDB`.
2. Back up the original source and merge the patch while preserving local changes.
3. Stop running backend. From project root run `powershell -ExecutionPolicy Bypass -File .\scripts\setup-auth.ps1`.
4. Script installs EF CLI (if needed), creates migration only if not present, applies migration to `StudentProjectsDev`, sets 48-byte random JWT key in .NET user secrets and prompts for dev Admin email/password (12+ chars).
5. Start API: `dotnet run --project backend/StudentProjects.Api` (in project root).
6. Start frontend: `cd frontend; npm install; npm run dev`.
7. Open `http://127.0.0.1:5173/login` and sign in with development admin credentials.
8. After successful first startup, remove the bootstrap password secret: `dotnet user-secrets remove 'AuthBootstrap:Password' --project backend/StudentProjects.Api`. The account remains in the database. Keep your actual Admin password safe.

Migration is **generated and executed locally** by the script. No pre-generated migration is checked in because .NET SDK is unavailable in the packaging environment. Review the generated initial migration and check it into version control. This initial migration creates ONLY Users and Roles; the other 20 draft tables are intentionally excluded via ExcludeFromMigrations(). **Do not use initial migration against an existing production database with tables/data.**

If LocalDB not available, supply a SQL Server connection string via `dotnet user-secrets set 'ConnectionStrings:Default' '...' --project backend/StudentProjects.Api` and run the EF commands manually. See `scripts/setup-auth.ps1` for syntax.

## Integration tests

`dotnet test backend/StudentProjects.IntegrationTests/StudentProjects.IntegrationTests.csproj`

Uses EF Core InMemory provider with WebApplicationFactory to check invalid login, unauthorized, forbidden, current user, logout revocation and disabled accounts. This does NOT test SQL Server relational constraints or SQL migration; those require a separate database-backed test environment.

## Security / restrictions

- No registration endpoint: Admin credentials are seeded in Development with secrets. Student/Lecturer accounts and administration CRUD remain future modules.
- Keep JWT key, DB credentials and admin password outside Git; `appsettings.Development.json` contains only an example LocalDB connection.
- A valid JWT does not authorize access to another user's private data; ownership checks must be implemented with each future business handler.
- Login has IP-based fixed-window rate limiting (20/minute per remote IP); production reverse proxies need trusted forwarded header configuration.
- Authentication validates the DB every request for active user/role/token version, which trades extra DB reads for immediate revocation.
- No automatic EF migrations in the API startup path. Apply migrations deliberately using CLI.
- Logout is all-session revocation; multi-session selective logout, refresh tokens, account lockout, password reset and email verification are outside this scope.
- Do not use Development bootstrap/secrets flow as a production user-provisioning process.
