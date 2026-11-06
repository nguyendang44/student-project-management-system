# Git Progress Diagnostics v3.1

**Purpose:** Determine exactly why Ollama generated 12 candidate observations but the v3 filter retained 0. Does **not** loosen citation validation or meaning screening. Does **not** change SQL Server schema or erase saved reports.

## Changes

- `AiGitProgressClient.cs`: creates counts for first rejection reason of each candidate, changes analysis key to `gp-diagnostics-v3.1` so the same two commits can be analyzed again without returning the v3 cached result.
- `GitProgressMeaningPolicy.cs`: adds `ExplainRejectionsById` which mirrors the existing screening checks without modifying them.
- `GitProgressEvidencePolicy.cs`: extends `ProgressAiResult` with optional diagnostics counts.
- `GitProgressWorker.cs`: logs a summary of counts without source code, raw Ollama output, credentials or secrets.
- `GitProgressMeaningfulTests.cs`: adds two regression tests and updates the expected version string.

Counts classify candidates by the first failed condition: `unknownId`, `unsentFile`, `unsentLine`, `nonFunctionalLine`, `weakDescription`, `duplicate`. `eligibleBeforeCap` means candidates passing the checks before per-file limits are applied. Counts add up to `AI candidates` if there are no unparsed entries; `AI candidates` here counts only successfully parsed JSON objects.

## Installation

Extract directory `v011-git-progress-diagnostics-v31` to the repository root, alongside `backend` and `frontend`.

Stop the API, then in PowerShell at the repository root:

```powershell
git branch --show-current
git status --short
py .\v011-git-progress-diagnostics-v31\apply_progress_diagnostics.py . --check
py .\v011-git-progress-diagnostics-v31\apply_progress_diagnostics.py .
dotnet build backend
dotnet test backend
```

The installer only applies to an **exact v3 baseline**. If `--check` fails, stop. Don't run `git reset --hard`. It creates backups outside the repository.

Restart the API with output captured:

```powershell
dotnet run --project .\backend\StudentProjects.Api 2>&1 | Tee-Object -FilePath .\git-progress-debug.log
```

Open `/git-progress` and retry the same pair (v3.1 has a new analysis key):

- base `dadc6d42b74d16a532def7e2d8deda8db5f206fa`
- head `fdba846c37f2cc824dfa09fff6938a260a6268df`

In another PowerShell terminal:

```powershell
Select-String .\git-progress-debug.log -Pattern 'AI candidates=|rejected unknownId=' | ForEach-Object { $_.Line }
```

Report the two lines. **Do not** share raw Ollama prompts or diffs if they contain secrets.

## Diagnostics interpretation

- `unknownId > 0`: model used IDs not present in input. Fix prompt/ID selection, not safety guards.
- `unsentLine > 0`: citation cannot be matched to an allowed diff line. Keep hard rejection.
- `nonFunctionalLine > 0`: model referred to a comment/declaration instead of a functional statement.
- `weakDescription > 0`: `UsefulDescription()` rejects generic or incorrectly phrased descriptions; tune the meaning policy based on actual safe examples.
- `duplicate > 0`: redundant observations.
- `eligibleBeforeCap > 0` but `verified == 0`: investigate the selection cap logic.

No SQL migration or frontend update needed. Validate with `dotnet build` / `dotnet test` on Windows; this authoring environment lacks the .NET SDK.
