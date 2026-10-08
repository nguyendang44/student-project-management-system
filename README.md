# Student Project Management System v0.3

Based on the Week 1 and Week 2 business analysis and Week 3 schema proposal.

- Frontend Vue 3 + TypeScript + Pinia: functional login, role-dependent navigation, logout (memory-only access token).
- Backend ASP.NET Core 10: functional `auth/login`, `auth/me`, `auth/logout`, JWT authentication and backend role authorization.
- Database SQL Server (LocalDB for Windows development), EF Core relational Users/Roles plus remaining schema draft.
- Auth integration tests via WebApplicationFactory + EF Core InMemory.
- Remaining modules and APIs are **not implemented** and return 501 after access checks.

**Start here:** [docs/AUTH-IMPLEMENTATION.md](docs/AUTH-IMPLEMENTATION.md).

The initial EF Core migration (Users and Roles only) MUST be generated and applied on your Windows development machine. The generated migration is not part of this package. A fresh database is required for this setup.
