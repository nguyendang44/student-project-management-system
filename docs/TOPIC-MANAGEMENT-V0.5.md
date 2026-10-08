# Topic Management v0.5 | Functional Implementation

Source of truth: week 1 FR-06, FR-07 and week 2 UC-05 to UC-10, UC-41.

## Scope
- Admin and Lecturer: create approved topics and update their information (Lecturer: own topics; Admin: all).
- Student: create draft topics, propose a topic, modify own draft/rejected proposal, resubmit for review.
- Lecturer: approve/reject pending student proposals; rejection requires a reason. TopicStateHistories records transitions; AuditEntries records important actions.
- Admin and lecturer author: open/close registration for approved topics only.
- Admin: create registration period and open/close registration period.
- Student: register an approved/open topic within an active/open period, view own registrations, cancel pending request. Database unique filtered index prevents two active registrations in one period even for concurrent requests.
- Roles: Students cannot read other students' drafts or pending proposals. Lecturer can review pending proposals; Students cannot approve their own proposals. Admin cannot use Lecturer approval endpoint.

## Differences/limitations
- Topic proposal state is stored in Topic.Status; there is no duplicate Proposal table.
- Topic registrations stay PENDING (until future lecturer assignment workflow). No Project is created at this stage.
- Topic registration request is distinct from Lecturer Request; the latter remains skeleton.
- There is no accepted/rejected TopicRegistration action in v0.5; confirmed guide assignment is a future step.
- Hard deletion/cancellation of a topic proposal is deferred because the functional specification does not define cancellation history/retention rules. Cancelling a pending *topic registration* is supported.
- Tests use EF InMemory and cannot prove SQL Server query translation or migration. Before acceptance, run real SQL Server smoke tests.
- Current topic review policy: any Lecturer can review a PENDING_APPROVAL proposal. Assignment to a specific lecturer is not defined in the analysis and requires a future rule.
- Approved topics do not automatically become available: Admin (or lecturer author) explicitly opens registration.

## DB changes (only four tables)
`Topics`, `TopicStateHistories`, `TopicRegistrations`, `RegistrationPeriods`.
The existing Users/Roles/Students/Lecturers/AuditEntries remain unchanged.

### Installation on existing Windows project v0.4
1. Backup database + repository, stop Backend before file copy/build.
2. Copy *contents* of patch into `C:\Users\dawn\student-project-management-system` (leave existing migrations intact).
3. Run `powershell -ExecutionPolicy Bypass -File .\scripts\prepare-topics-v05.ps1` from project root.
4. Check that script prints `Build OK. Preview verified: 4 tables. No database changes have been made.`. Inspect `topics-v05-preview.sql`.
5. Only after DB backup and SQL inspection:
   `dotnet ef database update --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext`
6. `dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj`
7. Start API: `dotnet run --project .\backend\StudentProjects.Api`.
8. Start frontend from `frontend`: `npm install` (only if needed), `npm run dev`.

**Do not** rerun setup-auth, recreate Users/Roles, delete SQL Server database or rename migrations. Script `prepare-topics-v05.ps1` creates a migration in the user's project and preserves EF-generated Designer/ModelSnapshot.

## Real SQL Server smoke test
1. Admin creates period starting yesterday, ending tomorrow, open=true.
2. Admin creates an approved topic and opens registration.
3. Student registers once and receives 201; second registration same period receives 409; cancellation releases registration slot.
4. Student creates proposal, Lecturer rejects with reason, Student edits/resubmits, Lecturer approves.
5. Verify student cannot see another student's draft or modify their topic. Verify Admin cannot approve on Lecturer endpoint.
6. Refresh pages; ensure persistent rows and zero HTTP 500.

## Notes for subsequent migrations
The initial snapshots of this project included excluded draft tables. This v0.5 migration uses an EF-generated designer and snapshot plus a scoped hand-reviewed Up/Down replacement. Do not scaffold a second `TopicManagementV05` migration after running the script. Later migrations must build from the v0.5 snapshot.
