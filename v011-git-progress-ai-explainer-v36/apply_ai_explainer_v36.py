#!/usr/bin/env python3
"""Safe, one-time v3.5 -> v3.6 Git Progress AI explanation upgrade."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
MODIFIED = [
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'frontend/src/features/gitProgress/gitProgress.api.ts',
    'frontend/src/views/GitProgressV11View.vue',
]
NEW = [
    'backend/StudentProjects.Api/Services/GitProgressAiExplanationPolicy.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressAiExplanationV36Tests.cs',
]
VERSION_TESTS = [
    'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressGroundedFallbackV34Tests.cs',
]
OLD = '|gp-features-v3.5'
NEW_KEY = '|gp-explain-v3.6'


def prepare(root: Path) -> dict[str, str]:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Run from the student-project-management-system repository root.')
    updated: dict[str, str] = {}
    for rel in MODIFIED:
        target = root / rel
        expected = HERE / 'expected' / rel
        payload = HERE / 'payload' / rel
        if not target.is_file() or not expected.is_file() or not payload.is_file():
            raise ValueError(f'Missing source or payload: {rel}')
        if target.read_text(encoding='utf-8-sig') != expected.read_text(encoding='utf-8-sig'):
            raise ValueError(f'{rel}: file differs from v3.5; refusing to overwrite your edits')
        updated[rel] = payload.read_text(encoding='utf-8-sig')
    for rel in NEW:
        if (root / rel).exists():
            raise ValueError(f'File already exists: {rel}; refusing to overwrite')
        source = HERE / 'payload' / rel
        if not source.is_file():
            raise ValueError(f'Missing payload: {rel}')
        updated[rel] = source.read_text(encoding='utf-8-sig')
    for rel in VERSION_TESTS:
        target = root / rel
        if not target.is_file():
            raise ValueError(f'Missing regression tests: {rel}')
        value = target.read_text(encoding='utf-8-sig')
        if value.count(OLD) != 1 or NEW_KEY in value:
            raise ValueError(f'{rel}: expected one {OLD} assertion')
        updated[rel] = value.replace(OLD, NEW_KEY, 1)
    return updated


def install(root: Path, updated: dict[str, str]) -> Path:
    backup = root.parent / ('v011-gp-v36-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in MODIFIED + VERSION_TESTS:
        target = backup / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, target)
    changed: list[str] = []
    try:
        for rel, content in updated.items():
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            temp = target.with_name(target.name + '.v36-tmp')
            temp.write_text(content, encoding='utf-8')
            temp.replace(target)
            changed.append(rel)
    except OSError:
        for rel in reversed(changed):
            target = root / rel
            if rel in NEW:
                target.unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, target)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Root of student-project-management-system')
    parser.add_argument('--check', action='store_true', help='Validate without writing')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    updated = prepare(root)
    if args.check:
        print(f'CHECK OK: v3.5-compatible; {len(updated)} files validated, no changes written.')
    else:
        backup = install(root, updated)
        print('APPLIED: Git Progress AI Explainer v3.6.')
        print('BACKUP:', backup)
        print('NEXT: dotnet build backend; dotnet test backend; cd frontend; npm run build')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        sys.exit(1)
