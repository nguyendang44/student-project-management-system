# Git Progress Meaningful Descriptions v3.2

## Why this patch exists

v3.1 diagnosis for an actual run: `AI candidates=12, verified=0`, with `weakDescription=12` and all evidence errors at 0. The real regression is a narrow list of accepted verbs in `GitProgressMeaningPolicy.UsefulDescription` combined with prompts that permit freer wording.

v3.2 expands the behavioral-verb detector for ordinary English and Vietnamese expressions, continues to reject hollow class-declaration summaries and unsupported runtime/security claims, and gives Qwen2.5-Coder more explicit examples of concise Vietnamese observations. `AiGitProgressClient.AnalysisKey` now uses `gp-meaning-v3.2` to permit re-analysis of the *same* commit pair without returning the v3.1 cached report.

**Does not** change evidence ID validation, GitHub diff retrieval, SQL Server, schema/migrations, the frontend or history. **Does not** guarantee any accepted AI findings: if the model emits only hollow observations, 0 findings remains the correct outcome.

## Installation (Windows PowerShell)

Unzip `v011-git-progress-meaning-v32` alongside `backend` and `frontend`. From repository root, stop the backend and run:

```powershell
git branch --show-current
py .\v011-git-progress-meaning-v32\apply_meaning_v32.py . --check
py .\v011-git-progress-meaning-v32\apply_meaning_v32.py .
dotnet build backend
dotnet test backend
```

`--check` must print `CHECK OK` before applying. The installer requires the *exact v3.1 production files*, refuses accidental overwrites of local changes, backs up replaced files **outside** the repository and updates only the version-assertion strings in existing test files. It preserves your manual corrections to the old test literals. One new regression-test file is added.

Restart the backend:

```powershell
dotnet run --project .\backend\StudentProjects.Api 2>&1 | Tee-Object -FilePath .\git-progress-debug.log
```

Re-run `/git-progress` with base `dadc6d42b74d16a532def7e2d8deda8db5f206fa` and head `fdba846c37f2cc824dfa09fff6938a260a6268df`. Then in a **second** PowerShell window:

```powershell
Select-String .\git-progress-debug.log -Pattern 'AI candidates=|rejected unknownId=' | ForEach-Object { $_.Line }
```

Compare `candidates`, `weakDescription`, `eligibleBeforeCap` and `verified`. For any accepted observations, read the actual summary and check they genuinely describe work evidenced by the referenced line rather than restating declarations. Avoid committing unless .NET tests and runtime behavior are satisfactory.

## Validation status

The installer and text transformations have automated Python tests. **C# compilation and Ollama runtime behavior must be verified on the Windows development machine.**
