# ERD draft (entity inventory only)

Provisional records: `User`, `StudentProfile`, `LecturerProfile`, `RegistrationPeriod`, `Topic`, `TopicStateHistory`, `TopicRegistration`, `LecturerRequest`, `Project`, `Milestone`, `ProgressUpdate`, `MilestoneSubmission`, `ProjectEvaluation`, `RepositoryLink`, `CodeAnalysisReport`, `Notification`, `AutomationRun`, `SystemError`, `AuditEntry`, `SystemSetting`.

Key relationships to validate in Week 3:

- `User` 1:0..1 `StudentProfile` / `LecturerProfile` (mutually exclusive based on role).
- `Topic` belongs to proposer (`User`), can have `TopicStateHistory` and `TopicRegistration`.
- `LecturerRequest` links Student, Lecturer, Topic, RegistrationPeriod; acceptance may trigger one `Project`.
- `Project` links Topic, Lecturer and Student (group-member scenarios remain **unspecified** by source documents).
- `Project` 1:N `Milestone`; `Milestone` 1:N `ProgressUpdate` / `MilestoneSubmission`.
- `Project` 1:N `RepositoryLink` / `CodeAnalysisReport` / `ProjectEvaluation`.
- `Notification`, `AuditEntry`, `AutomationRun`, `SystemError`, `SystemSetting` support cross-cutting use cases.

Important invariants **not implemented yet**:
- Active student must not have conflicting topic registrations / assigned lecturer.
- Lecturer capacity must be checked server-side both when making and accepting requests; acceptance must be atomic even under concurrency.
- A student may update only their own project milestones; only the assigned lecturer may approve.
- Status transitions follow Week 2 tables. `REJECTED` belongs to Topic Proposal or request, never to `Project`.
- Milestone deadline is timestamp data; `OVERDUE` is the applicable milestone status.

The DbContext currently contains a **draft set of entities**, indexes, and a SQL capacity check constraint, but no migration and no completed relations/constraints. Review this document before production schema development.
