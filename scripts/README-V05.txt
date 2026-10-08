Topic Management v0.5
=====================
From project root (PowerShell):
  powershell -ExecutionPolicy Bypass -File .\scripts\prepare-topics-v05.ps1

DO NOT run the script again after successful migration generation.
DO NOT rerun setup-auth.ps1, delete the database or delete older migrations.

After backup and manual preview review:
  dotnet ef database update --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext
  dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj

Full documentation: docs/TOPIC-MANAGEMENT-V0.5.md
