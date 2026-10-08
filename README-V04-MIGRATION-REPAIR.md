# v0.4 migration repair (no database reset)

## Why the earlier script stopped

`UserManagementV04Schema` was generated without the expected `CreateTable` operations. EF Core can generate an empty migration when `ExcludeFromMigrations()` is removed for tables that were already mapped in the model snapshot. The first script correctly refused to apply an unknown migration.

## Steps

1. Back up the repository and SQL Server LocalDB database. Stop `StudentProjects.Api` to avoid the DLL file-lock error.
2. Copy the `scripts/repair-users-v04-migration.ps1` file from this archive to the matching project folder.
3. From project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\repair-users-v04-migration.ps1
```

This script only repairs a **previously generated, empty, unapplied** `UserManagementV04Schema` migration. It refuses to overwrite a nonempty migration. It preserves the migration ID, designer, and model snapshot; inserts explicit `CreateTable`, indexes, and FK operations for Students, Lecturers, and AuditEntries; backs up the migration; builds the solution; generates `users-v04-schema-preview.sql`; and checks that the script creates only the three intended tables. It **does not apply any SQL to the database**.

4. If it prints `Build OK. SQL preview verified`, review `users-v04-schema-preview.sql` and execute:

```powershell
dotnet ef database update --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext
```

5. Restart Backend, reload the Users page and run tests.

## If the repair stops

Do not remove migrations, delete a database, or run the initial auth setup again. Send the generated `...UserManagementV04Schema.cs` and diagnostic/error output for review. This patch cannot be runtime-tested against your Windows SQL Server from here.
