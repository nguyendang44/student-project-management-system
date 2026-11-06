#!/usr/bin/env python3
"""Safely upgrade Git Progress v3.3 to grounded-diff fallback v3.4."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
SOURCES = [
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs',
    'frontend/src/views/GitProgressV11View.vue',
    'frontend/src/features/gitProgress/gitProgress.api.ts',
]
VERSION_TESTS = [
    'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
]
NEW = [
    'backend/StudentProjects.Api/Services/GitProgressGroundedFallback.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressGroundedFallbackV34Tests.cs',
]
OLD_KEY = '|gp-reasons-v3.3'
NEW_KEY = '|gp-grounded-v3.4'


def prepare(root: Path) -> dict[str, str]:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Run from the student-project-management-system repository root.')
    output = {}
    for rel in SOURCES:
        expected = HERE / 'expected' / rel
        payload = HERE / 'payload' / rel
        dest = root / rel
        if not dest.is_file() or not expected.is_file() or not payload.is_file():
            raise ValueError(f'Missing required file: {rel}')
        if dest.read_text(encoding='utf-8-sig') != expected.read_text(encoding='utf-8-sig'):
            raise ValueError(f'{rel} differs from v3.3; will not overwrite changes.')
        output[rel] = payload.read_text(encoding='utf-8-sig')
    for rel in VERSION_TESTS:
        dest = root / rel
        if not dest.is_file():
            raise ValueError(f'Missing test: {rel}')
        text = dest.read_text(encoding='utf-8-sig')
        if text.count(OLD_KEY) != 1 or NEW_KEY in text:
            raise ValueError(f'{rel}: expected exactly one {OLD_KEY} assertion.')
        output[rel] = text.replace(OLD_KEY, NEW_KEY, 1)
    for rel in NEW:
        if (root / rel).exists():
            raise ValueError(f'{rel} already exists, refusing to overwrite.')
        payload = HERE / 'payload' / rel
        if not payload.is_file():
            raise ValueError(f'Missing payload: {rel}')
        output[rel] = payload.read_text(encoding='utf-8-sig')
    return output


def apply(root: Path, changes: dict[str, str]) -> Path:
    backup = root.parent / ('v011-gp-grounded-v34-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True)
    for rel in SOURCES + VERSION_TESTS:
        dest = backup / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, dest)
    changed = []
    try:
        for rel, content in changes.items():
            dest = root / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            tmp = dest.with_name(dest.name + '.gp34-tmp')
            tmp.write_text(content, encoding='utf-8')
            tmp.replace(dest)
            changed.append(rel)
    except OSError:
        for rel in changed:
            if rel in NEW:
                (root / rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    changes = prepare(root)
    if args.check:
        print(f'CHECK OK: v3.3 compatible; {len(changes)} files prepared; no changes made.')
    else:
        backup = apply(root, changes)
        print('APPLIED: Git Progress grounded evidence fallback v3.4.')
        print('BACKUP:', backup)
        print('NEXT: dotnet build backend; dotnet test backend; npm run build (frontend).')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        raise SystemExit(1)
