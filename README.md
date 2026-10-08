# Student Project Management System v0.4

Based on the Week 1 and Week 2 business analysis and Week 3 schema proposal.

- Frontend Vue 3 + TypeScript + Pinia: functional login, role-dependent navigation, logout, Admin user management and Student/Lecturer self-profile editing (memory-only access token).
- Backend ASP.NET Core 10: functional authentication + User Management API (Student/Lecturer only), JWT authentication and backend role authorization.
- Database SQL Server (LocalDB for Windows development); existing EF Core User, Role, StudentProfile, LecturerProfile and AuditEntry mappings. Confirm local initial migration created the required tables.
- Auth and User Management integration tests via WebApplicationFactory + EF Core InMemory.
- Remaining non-user-management modules and APIs are **not implemented** and return 501 after access checks.

**v0.4 installation:** [docs/USER-MANAGEMENT-V0.4.md](docs/USER-MANAGEMENT-V0.4.md). Previous authentication documentation: [docs/AUTH-IMPLEMENTATION.md](docs/AUTH-IMPLEMENTATION.md).

v0.4 must be installed over the **existing** v0.3 database without deleting or resetting it. The v0.3 migration was generated locally on Windows and is not included here; inspect the migration and confirm the required tables are already present. Do not re-run the initial Admin bootstrap script.
