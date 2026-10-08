# QA | Skeleton v0.1

## Verified in the preparation environment

- Passed `scripts/verify_scaffold.py`: 43/43 generic Use Case identifiers found in API skeleton; 23 frontend modules; 23 corresponding feature clients plus auth; 53 distinct API method/path declarations; 20 draft entities; 4 C# projects; no duplicated endpoint method/path; mutations for lecturer approval, lecturer acceptance and student submission use the intended role boundary.
- Passed Vue/TypeScript static type check with `vue-tsc --noEmit` using installed Vue toolchain files extracted to a **temporary QA directory** from the supplied Lab Booking frontend archive. QA dependencies are not part of the deliverable.
- Did not perform a .NET compile, because .NET SDK is not installed in the preparation environment.
- Did not complete a production frontend bundle: the temporary frontend archive includes native Rolldown dependencies built for Windows, not Linux. `npm install` in the preparation environment could not complete; with network access use the fresh package versions from `frontend/package.json` and run `npm run build`.
- No live browser end-to-end smoke test was performed; do not assume runtime UI/API integration has passed.

## Security and implementation notes

The frontend `Student`/`Lecturer`/`Admin` selector controls only visible preview menus. It is not an authenticated account or a security test. All API business routes are explicit 501 handlers, protected by the declared JWT/role boundary where appropriate; the login route itself returns 501, and no tokens are issued. Real authentication, ownership checks, capacity transactions, migrations and jobs must be implemented before deployment.

## Week 3 (v0.2) QA supplement

- **Scope:** only domain entity model, proposed EF Core mapping, architecture/ERD/class-diagram docs, upgrade instructions and a static verifier. Frontend source and API endpoint implementation remain unchanged from v0.1.
- **Structural check:** `python scripts/verify_database_design.py` passed. 21 domain entity classes = 21 DbSets = 21 distinct tables in diagrams; 33 foreign-key fields have Fluent API `HasForeignKey` mappings; five named SQL check constraints present.
- **Invariant separation:** per-registration-period lecturer capacity plus rowversion/check constraint added at data-model level, without claiming concurrency-safe acceptance has been implemented. Workflow invariants remain TODOs for Application-level transaction tests.
- **Not verified in authoring environment:** no `dotnet` executable; .NET 10 compilation, EF Core migration generation, SQL Server integration, SQL check constraint behavior, and real concurrent requests have NOT been tested. User should execute `dotnet build backend/StudentProjects.sln` locally before treating model mapping as build-verified.
- **No API/database behavior change:** `/health` and 501 endpoints remain as they were. No SQL connection required to start the skeleton without a configured connection string. No new migrations created.
- **Open design decisions:** see `docs/DATABASE-DESIGN.md`, especially team-project membership, one-to-many vs one-to-one GitHub links, registration uniqueness under rejection/withdrawal, and per-period lecturer capacity policy.
