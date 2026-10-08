# QA | Skeleton v0.1

## Verified in the preparation environment

- Passed `scripts/verify_scaffold.py`: 43/43 generic Use Case identifiers found in API skeleton; 23 frontend modules; 23 corresponding feature clients plus auth; 53 distinct API method/path declarations; 20 draft entities; 4 C# projects; no duplicated endpoint method/path; mutations for lecturer approval, lecturer acceptance and student submission use the intended role boundary.
- Passed Vue/TypeScript static type check with `vue-tsc --noEmit` using installed Vue toolchain files extracted to a **temporary QA directory** from the supplied Lab Booking frontend archive. QA dependencies are not part of the deliverable.
- Did not perform a .NET compile, because .NET SDK is not installed in the preparation environment.
- Did not complete a production frontend bundle: the temporary frontend archive includes native Rolldown dependencies built for Windows, not Linux. `npm install` in the preparation environment could not complete; with network access use the fresh package versions from `frontend/package.json` and run `npm run build`.
- No live browser end-to-end smoke test was performed; do not assume runtime UI/API integration has passed.

## Security and implementation notes

The frontend `Student`/`Lecturer`/`Admin` selector controls only visible preview menus. It is not an authenticated account or a security test. All API business routes are explicit 501 handlers, protected by the declared JWT/role boundary where appropriate; the login route itself returns 501, and no tokens are issued. Real authentication, ownership checks, capacity transactions, migrations and jobs must be implemented before deployment.
