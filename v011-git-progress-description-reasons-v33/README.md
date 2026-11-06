# Git Progress v3.3: precise reasons for `weakDescription`

**Problem observed:** v3.2 produced 12 parsed Ollama candidates, all 12 rejected as `weakDescription` with valid Git evidence IDs, files, and lines. This category combines different causes. The actual model descriptions have not been inspected. It would be unsafe to relax the filter based solely on the aggregate count.

This patch makes `GitProgressMeaningPolicy.CheckDescription()` report one of: `InvalidLength`, `UnsupportedClaim`, `Boilerplate`, `MissingAction`, `None`. `UsefulDescription()` delegates to it, preserving v3.2 acceptance logic. The worker logs only aggregated counts, never raw diffs, candidate descriptions or credentials. The analysis key changes to `gp-reasons-v3.3` so the same SHA pair runs again without old cached results.

## Installation (Windows PowerShell)

Unzip next to `backend` and `frontend`, from repository root. Stop your running backend.

```powershell
py .\v011-git-progress-description-reasons-v33\apply_description_diagnostics_v33.py . --check
py .\v011-git-progress-description-reasons-v33\apply_description_diagnostics_v33.py .
dotnet build backend
dotnet test backend
```

Run the backend while capturing logs:

```powershell
dotnet run --project .\backend\StudentProjects.Api 2>&1 | Tee-Object -FilePath .\git-progress-debug.log
```

In `/git-progress`, rerun the SAME pair `dadc6d42b74d16a532def7e2d8deda8db5f206fa` to `fdba846c37f2cc824dfa09fff6938a260a6268df` (different analysis key avoids cache). In a second PowerShell:

```powershell
Select-String .\git-progress-debug.log -Pattern 'weakDescription reasons|AI candidates=|rejected unknownId=' | ForEach-Object {$_.Line}
```

Compare `invalidLength`, `unsupportedClaim`, `boilerplate`, and `missingAction`. They must sum to `weakDescription`. Based on the results we can revise the prompt or the correct rule, instead of introducing false positives.

**No database migrations or changes to report storage.** Existing history remains. Strict compatibility checks refuse to overwrite unexpected changes. Backups are outside the repository. New integration tests verify the reason taxonomy and count invariants. Tested on the Python side only; .NET SDK and your Ollama instance must run on Windows to verify C# and runtime behavior.
