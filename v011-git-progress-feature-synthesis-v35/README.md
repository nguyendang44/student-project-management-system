# Git Progress v3.5 — Feature Synthesis

This is an incremental upgrade **from v3.4 only**. It does not create a SQL migration and does not delete previous reports.

## Changes

- Recognizes **production API routes** from endpoint files and never treats a test's `MapPost("/login", ...)` as a newly deployed API.
- Recognizes HTTP model calls and the background processing queue from concrete service lines, not bare `SaveChangesAsync` calls.
- Groups **frontend UI events with frontend API calls**, when their lines occur in the GitHub diff.
- Extracts **actual names of newly declared database tables** from the migration instead of generic `CreateTable` counts.
- Labels **tests as test code**, not runtime production features or test execution results.
- Includes **multiple exact citations across multiple changed files** in each feature report, subject to safety filtering. Facts are computed directly from safe returned GitHub diffs, independently of the 10-file subset sent to Ollama.
- Keeps legacy report JSON readable, version-tags analysis `gp-features-v3.5` to avoid cached v3.4 reports, and preserves independently verified AI observations when any exist.

**Limits:** Reports describe observable code changes, not runtime success, feature completion, or grades. A category is omitted when suitable cited lines are not found. GitHub compare and diff coverage can be incomplete. Ollama findings still undergo the prior strict filters.

## Installation (PowerShell at repository root)

Stop the running backend before patching.

```powershell
py .\v011-git-progress-feature-synthesis-v35\apply_feature_synthesis_v35.py . --check
py .\v011-git-progress-feature-synthesis-v35\apply_feature_synthesis_v35.py .
dotnet build backend
dotnet test backend
cd frontend
npm run build
cd ..
```

If any test fails, do not commit. The installer creates a separate backup folder next to the repository. No API keys, Git tokens, SQL data, or generated logs are included in this ZIP.

Start the backend and repeat `dadc6d4 → fdba846` at `/git-progress`. The new report should be tagged `gp-features-v3.5`. Look for groups **API**, **AI/Backend**, **Frontend**, **CSDL**, **Kiểm thử**, each containing exact evidence lines when present in the compared GitHub diff. The report keeps the original Git stats and identifies feature synthesis as **rule-based Git evidence, not Ollama-generated judgments**.

The implementation deliberately avoids changing schema, entities, or migration history. **Do not re-run `dotnet ef migrations add`.**
