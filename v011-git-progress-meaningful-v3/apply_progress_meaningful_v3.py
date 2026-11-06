#!/usr/bin/env python3
"""Safely patch Git Progress Evidence ID v2 to Meaningful Progress v3.

Windows PowerShell at repository root:
  py .\v011-git-progress-meaningful-v3\apply_progress_meaningful_v3.py . --check
  py .\v011-git-progress-meaningful-v3\apply_progress_meaningful_v3.py .
"""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
EXPECTED = HERE / 'expected'
PAYLOAD = HERE / 'payload'
EDITED = (
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',
    'frontend/src/views/GitProgressV11View.vue',
    'frontend/src/features/gitProgress/gitProgress.api.ts',
)
ADDED = (
    'backend/StudentProjects.Api/Services/GitProgressMeaningPolicy.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
)


def prepare(root: Path) -> None:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Repository root not found (expected .git and backend/StudentProjects.Api/Program.cs)')
    for rel in EDITED:
        target = root / rel
        expected = EXPECTED / rel
        payload = PAYLOAD / rel
        if not target.is_file() or not expected.is_file() or not payload.is_file():
            raise ValueError(f'Missing baseline or patch content for: {rel}')
        current = target.read_text(encoding='utf-8-sig')
        before = expected.read_text(encoding='utf-8-sig')
        after = payload.read_text(encoding='utf-8-sig')
        if current == after:
            raise ValueError(f'v3 already applied: {rel}')
        if current != before:
            raise ValueError(f'File differs from expected v2; refusing overwrite: {rel}')
    for rel in ADDED:
        if (root / rel).exists():
            raise ValueError(f'Patch target already exists: {rel}')
        if not (PAYLOAD / rel).is_file():
            raise ValueError(f'New file missing from payload: {rel}')


def apply(root: Path) -> Path:
    backup = root.parent / ('v011-git-progress-meaningful-v3-backup-' +
                            datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in EDITED:
        dest = backup / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, dest)
    applied = []
    try:
        for rel in EDITED + ADDED:
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            temp = target.with_name(target.name + '.gp-v3-temp')
            temp.write_text((PAYLOAD / rel).read_text(encoding='utf-8-sig'), encoding='utf-8')
            temp.replace(target)
            applied.append(rel)
    except OSError:
        for rel in reversed(applied):
            if rel in ADDED:
                (root / rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Path to project repository root')
    parser.add_argument('--check', action='store_true', help='Validate without changing project files')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    prepare(root)
    print(f'Validated: {len(EDITED)} files updated + {len(ADDED)} files added (no SQL migration)')
    for rel in EDITED:
        print('~', rel)
    for rel in ADDED:
        print('+', rel)
    if args.check:
        print('CHECK OK: repository unchanged')
        return
    backup = apply(root)
    print('APPLIED v3. Backup outside repository:', backup)
    print('NEXT: dotnet build backend; dotnet test backend; npm run build in frontend; restart API.')
    print('Re-analyze same SHAs: v3 uses a new analysis key, preserving old reports.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError) as error:
        print('ERROR:', error, file=sys.stderr)
        raise SystemExit(1)
