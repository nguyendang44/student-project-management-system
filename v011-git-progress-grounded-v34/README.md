# Git Progress v3.4: grounded diff fallback

## Why

The runtime diagnostics of v3.3 proved `AI candidates=12`, `verified=0`, with `boilerplate=12`, and zero bad evidence IDs or unsent lines. Ollama supplied only low-value descriptions, so **the filter was correctly rejecting them**. Do not loosen the screening criteria to inflate the number of AI findings.

This patch keeps the AI filter unchanged. If zero AI findings pass it, the backend uses **conservative, deterministic rules** to identify certain syntactic edits from the exact changed diff lines already sent to the model. It can recognize new endpoint mappings, service registrations, UI events/bindings, EF database declarations, migration table operations, test assertions, and a limited set of async calls. Each observation is traceable to the original line. It **does not assert successful execution, completion, security, or test pass**.

The saved report gives every fallback item `source: "diff-rule"` rather than `source: "ollama"`, and the UI labels it accordingly. Historical reports remain readable. When no supported line exists, the report still correctly shows zero observations rather than inventing progress.

## Install on Windows

1. Stop backend. Unzip `v011-git-progress-grounded-v34` beside `backend` and `frontend` inside `student-project-management-system`.
2. From the repository root:

```powershell
py .\v011-git-progress-grounded-v34\apply_grounded_v34.py . --check
py .\v011-git-progress-grounded-v34\apply_grounded_v34.py .
dotnet build backend
dotnet test backend
cd frontend
npm run build
cd ..
```

3. If all pass, restart backend, rerun the same SHA pair and inspect the new `|gp-grounded-v3.4` report. The analysis key changes to avoid returning the old cached report.

```powershell
dotnet run --project .\backend\StudentProjects.Api 2>&1 | Tee-Object -FilePath .\git-progress-debug.log
```

Use these full commit SHAs:

- Base `dadc6d42b74d16a532def7e2d8deda8db5f206fa`
- Head `fdba846c37f2cc824dfa09fff6938a260a6268df`

To inspect log:

```powershell
Select-String .\git-progress-debug.log -Pattern 'AI candidates=|ruleBasedFallback=|weakDescription reasons' | ForEach-Object { $_.Line }
```

## Scope / limitations

- No new EF migrations or schema changes. Existing historical reports are preserved.
- Installer checks files against v3.3, refuses local modifications and duplicate installs, and creates a backup **outside** the repository.
- Five added C# regression tests verify multi-area facts, evidence restrictions, fallback activation, preserving legitimate AI observations, and avoiding false claims from declarations.
- Installer self-tests run under Python. **C# / Vue build and runtime with Ollama must be verified on the user's .NET/Windows machine**.
- This is a safe fallback, not a substitute for deep semantic reasoning over the full repository. Results depend on the selected changed lines and recognized syntax.
