# User Management v0.4

## Scope

This increment implements FR-03 (Student accounts), FR-04 (Lecturer accounts), and self-profile editing on top of previously functional Authentication/JWT. The login, JWT issuance, and roles from v0.3 remain unchanged.

- Admin can list, search, paginate, create, edit, disable and re-enable **Student** and **Lecturer** accounts. An Admin cannot create or alter another Admin through these endpoints.
- Users can update only their own display name and relevant academic profile fields via `/users/me/profile`. Email, StudentCode, Role, IsActive, and password cannot be changed via the self-profile endpoint.
- Authenticated users can see the limited directory of **active lecturers** (name and specialty; no email). Capacity information remains in a different future module.
- Backend validates role and account ownership. Any status change invalidates existing JWTs by incrementing TokenVersion. Email changes also invalidate JWTs.
- Passwords are hashed using ASP.NET Core `PasswordHasher<User>`; hashes are never serialized to API clients.
- Writes produce AuditEntry records. The audit-log UI remains a skeleton.

## Endpoints

| Method | Endpoint | Access | Result |
|---|---|---|---|
| GET | `/api/v1/users?role=Student&search=...&page=1&pageSize=20` | Admin | Paginated users |
| GET | `/api/v1/users/{id}` | Admin | One managed user |
| POST | `/api/v1/users` | Admin | Creates Student/Lecturer with 12-128 character initial password |
| PUT | `/api/v1/users/{id}` | Admin | Updates immutable-role account and profile |
| PATCH | `/api/v1/users/{id}/status` | Admin | `{ "isActive": false }` to disable |
| GET | `/api/v1/users/me/profile` | Student/Lecturer | Own profile only |
| PUT | `/api/v1/users/me/profile` | Student/Lecturer | `{ "fullName": "...", "faculty": "...", "specialty": "..." }` |
| GET | `/api/v1/lecturers` | Any authenticated role | Active lecturer names and specialties |

Input validation failures return 400; duplicate email/code return 409; missing resources return 404; unauthorized and forbidden requests return 401/403. SQL Server uniqueness conflicts are handled in addition to existence checks. StudentCode is stored uppercase for uniqueness and consistency.

## Applying v0.4 to an existing v0.3 install

1. **Back up** the project. Stop Backend (`Ctrl+C` or stop `StudentProjects.Api`) before overwriting code and running build/test, to avoid DLL file locking.
2. Unzip the `v0.4-patch.zip` and copy the **contents** of its `student-project-management-system/` folder to the corresponding root `C:\Users\dawn\student-project-management-system`, merging directories.
3. Your SQL Server LocalDB database and Admin account should **not** be removed, reset, or re-seeded. Do not rerun `setup-auth.ps1`.
4. The v0.3 DbContext already declares `StudentProfile`, `LecturerProfile`, `AuditEntry`, `Users`, and `Roles`. This update does **not change the EF entity model** and therefore **does not require a new migration if these tables were created by your original v0.3 migration**. Since the migration was generated locally on Windows and is not included in the source archive, confirm these tables exist first. If any table is missing, inspect your local migration history and generate/review a non-destructive additive EF migration before running the feature. Do not wipe the database.
5. Build and test from repo root:

```powershell
dotnet build .\backend\StudentProjects.sln
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj
```

6. Start API, then frontend in **separate terminals**:

```powershell
dotnet run --project .\backend\StudentProjects.Api
# In another terminal:
cd .\frontend
npm install
npm run dev
```

7. Open `http://127.0.0.1:5173`, log in using your existing Admin account, open **Người dùng**, create one Student and one Lecturer. Sign in with each new account and verify **Hồ sơ của tôi**, then disable a test account and verify its token no longer works.

## Test plan

`UserManagementIntegrationTests` adds tests for:
- Admin create/search/edit/disable/reactivate and token revocation.
- Student cannot list/manage other accounts and can only edit own allowed profile fields.
- Duplicate email/code, invalid password, Admin self-creation blocked.
- Lecturer specialty editing and hiding disabled lecturers from directory.
- Anonymous endpoints return 401.

The existing 4 Auth integration tests should also continue to pass. Note that EF Core InMemory tests do not validate SQL Server-specific uniqueness/check constraints or transaction behavior. A SQL Server smoke test should be performed on Windows before production use.

## Remaining scope

Registration periods, lecturer capacity per period, topic/project registration, milestones, progress, evaluations, GitHub, AI and notifications remain skeleton endpoints returning 501. User Management is not an implementation of those workflows. Password reset, password change, email verification, and session management are future features. For production, add concurrency policy, monitoring and production-grade security operations.
