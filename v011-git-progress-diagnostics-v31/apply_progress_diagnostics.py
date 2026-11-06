#!/usr/bin/env python3
"""Apply evidence rejection diagnostics to an existing v3 Git Progress installation."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
FILES = (
    'backend/StudentProjects.Api/Services/GitProgressMeaningPolicy.cs',
    'backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs',
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
)


def prepare(root: Path) -> None:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Expected repository root (.git and backend/StudentProjects.Api/Program.cs).')
    for rel in FILES:
        target = root / rel
        before = HERE / 'expected' / rel
        after = HERE / 'payload' / rel
        if not target.is_file() or not before.is_file() or not after.is_file():
            raise ValueError(f'Missing required file: {rel}')
        current = target.read_text(encoding='utf-8-sig')
        if current == after.read_text(encoding='utf-8-sig'):
            raise ValueError(f'Diagnostics already applied: {rel}')
        if current != before.read_text(encoding='utf-8-sig'):
            raise ValueError(f'Modified or incompatible v3 source: {rel}; refusing to overwrite.')


def apply(root: Path) -> Path:
    backup = root.parent / ('v011-gp-diagnostics-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in FILES:
        dest = backup / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, dest)
    edited = []
    try:
        for rel in FILES:
            dest = root / rel
            temporary = dest.with_name(dest.name + '.gpdiag-tmp')
            temporary.write_text((HERE / 'payload' / rel).read_text(encoding='utf-8-sig'), encoding='utf-8')
            temporary.replace(dest)
            edited.append(rel)
    except OSError:
        for rel in edited:
            shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Root of student-project-management-system')
    parser.add_argument('--check', action='store_true', help='Check exact v3 baseline without editing any file')
    args = parser.parse_args()
    repo = Path(args.repository).expanduser().resolve()
    prepare(repo)
    if args.check:
        print('CHECK OK: v3 baseline matches; 5 C# files ready; no changes made.')
        return
    backup = apply(repo)
    print('APPLIED: Git Progress rejection diagnostics v3.1.')
    print('BACKUP:', backup)
    print('NEXT: dotnet build backend; dotnet test backend; restart API; run same commit pair.')
    print('LOG: rejected unknownId / unsentFile / unsentLine / nonFunctionalLine / weakDescription / duplicate.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError) as error:
        print('ERROR:', error, file=sys.stderr)
        raise SystemExit(1)
