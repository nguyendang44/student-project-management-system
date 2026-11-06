#!/usr/bin/env python3
"""Add Git Progress Analysis to the v0.11 v7 student-project-management-system.

Run from the repo root:
  py .\v011-git-progress-upgrade\apply_git_progress.py . --check
  py .\v011-git-progress-upgrade\apply_git_progress.py .

The installer does NOT modify EF migrations. Generate one with dotnet ef as in README.
"""
from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
PAYLOAD = HERE / 'payload'
PROGRAM = Path('backend/StudentProjects.Api/Program.cs')
DB = Path('backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs')
ROUTER = Path('frontend/src/app/router.ts')
VIEW = Path('frontend/src/views/CodeAnalysisV11View.vue')
AI = Path('backend/StudentProjects.Api/Services/AiCodeReviewClient.cs')


def once(source: str, before: str, after: str, description: str) -> str:
    count = source.count(before)
    if count != 1:
        raise ValueError(f'{description}: expected one compatible anchor, found {count}')
    return source.replace(before, after, 1)


def prepare(root: Path) -> dict[Path, tuple[str | None, str]]:
    if not (root / AI).is_file() or 'sampled-feature-v7-' not in (root / AI).read_text(encoding='utf-8-sig'):
        raise ValueError('Expected v0.11 v7 AI analysis to be installed')
    originals = {}
    for relative in (PROGRAM, DB, ROUTER, VIEW):
        path = root / relative
        if not path.is_file():
            raise ValueError(f'Required source not found: {relative}')
        originals[relative] = path.read_text(encoding='utf-8-sig')
    p, db, router, view = (originals[x] for x in (PROGRAM, DB, ROUTER, VIEW))
    if 'GitProgressRuns' in db or 'GitProgressWorker' in p or 'GitProgressV11View' in router:
        raise ValueError('Git Progress already appears installed; refusing to apply twice')

    p = once(p,
        'builder.Services.AddHostedService<CodeAnalysisWorker>();',
        '''builder.Services.AddHostedService<CodeAnalysisWorker>();
// V011_GIT_PROGRESS: immutable GitHub compare SHAs, local Ollama and persisted worker results.
builder.Services.AddHttpClient<GitHubProgressClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<AiGitProgressClient>(client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:11434/");
    client.Timeout = TimeSpan.FromMinutes(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<GitProgressWorker>();''',
        'Program service registration')
    p = once(p,
        'app.MapCodeAnalysisV11Endpoints();',
        '''app.MapCodeAnalysisV11Endpoints();
app.MapGitProgressV11Endpoints();''',
        'Program endpoint mapping')

    db = once(db,
        '    public DbSet<CodeAnalysisRun> CodeAnalysisRuns => Set<CodeAnalysisRun>();',
        '''    public DbSet<CodeAnalysisRun> CodeAnalysisRuns => Set<CodeAnalysisRun>();
    public DbSet<GitProgressRun> GitProgressRuns => Set<GitProgressRun>();''',
        'DB DbSet')
    db = once(db,
        '        modelBuilder.Entity<CodeAnalysisFinding>(e =>',
        '''        // V011_GIT_PROGRESS: separate table; CodeAnalysisRuns and prior migrations unchanged.
        modelBuilder.Entity<GitProgressRun>(e =>
        {
            e.Property(x => x.BaseSha).HasMaxLength(40).IsRequired();
            e.Property(x => x.HeadSha).HasMaxLength(40).IsRequired();
            e.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasMaxLength(24).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);
            e.Property(x => x.FilesJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.EvidenceJson).HasColumnType("nvarchar(max)").IsRequired();
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<RepositoryLink>().WithMany().HasForeignKey(x => x.RepositoryLinkId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.RepositoryLinkId, x.BaseSha, x.HeadSha, x.ModelName }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        });
        modelBuilder.Entity<CodeAnalysisFinding>(e =>''',
        'DB entity mapping')

    router = once(router,
        "meta: { title: 'Hồ sơ của tôi', allowedRoles: ['Student', 'Lecturer'] } }, ...moduleRoutes] },",
        """meta: { title: 'Hồ sơ của tôi', allowedRoles: ['Student', 'Lecturer'] } },
       { path: 'git-progress', component: () => import('../views/GitProgressV11View.vue'),
         meta: { title: 'Git Progress Analysis', allowedRoles: ['Student', 'Lecturer', 'Admin'] } },
       ...moduleRoutes] },""",
        'Vue route')
    view = once(view,
        '      <h2>AI Code Analysis</h2>',
        '''      <h2>AI Code Analysis</h2>
      <p><RouterLink to="/git-progress">Phân tích tiến độ Git giữa hai commit →</RouterLink></p>''',
        'AI view link')

    changes: dict[Path, tuple[str | None, str]] = {
        PROGRAM: (originals[PROGRAM], p),
        DB: (originals[DB], db),
        ROUTER: (originals[ROUTER], router),
        VIEW: (originals[VIEW], view),
    }
    for f in sorted(PAYLOAD.rglob('*')):
        if not f.is_file():
            continue
        relative = f.relative_to(PAYLOAD)
        if (root / relative).exists():
            raise ValueError(f'Refusing to overwrite existing file: {relative}')
        changes[relative] = (None, f.read_text(encoding='utf-8-sig'))
    return changes


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Path to student-project-management-system root')
    parser.add_argument('--check', action='store_true', help='Verify without writing any file')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    changes = prepare(root)
    print(f'Validated: {sum(old is not None for old, _ in changes.values())} edits + '
          f'{sum(old is None for old, _ in changes.values())} new files; no migrations written.')
    for relative, (old, _) in changes.items():
        print('~' if old is not None else '+', relative)
    if args.check:
        print('CHECK OK: no repository files modified.')
        return

    backup = root.parent / ('v011-git-progress-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for relative, (old, _) in changes.items():
        if old is None:
            continue
        dest = backup / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / relative, dest)
    applied = []
    try:
        for relative, (old, new) in changes.items():
            dest = root / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            temp = dest.with_name(dest.name + '.v011-progress-tmp')
            temp.write_text(new, encoding='utf-8')
            temp.replace(dest)
            applied.append(relative)
    except OSError:
        for relative in applied:
            old, _ = changes[relative]
            if old is None:
                (root / relative).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / relative, root / relative)
        raise
    print('Applied Git Progress Analysis. Backup:', backup)
    print('NEXT: dotnet build backend; generate and inspect EF migration, then database update; dotnet test backend.')
    print('See README.md. Frontend: /git-progress .')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        raise SystemExit(1)
