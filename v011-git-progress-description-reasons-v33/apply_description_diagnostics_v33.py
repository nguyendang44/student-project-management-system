#!/usr/bin/env python3
"""Inspect v3.2 weakDescription rejections without loosening screening rules."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
POLICY = 'backend/StudentProjects.Api/Services/GitProgressMeaningPolicy.cs'
CLIENT = 'backend/StudentProjects.Api/Services/AiGitProgressClient.cs'
WORKER = 'backend/StudentProjects.Api/Services/GitProgressWorker.cs'
EVIDENCE_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs'
MEANING_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs'
NEW_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressDescriptionDiagnosticsV33Tests.cs'
PRODUCTION = (POLICY, CLIENT, WORKER)
TESTS = (EVIDENCE_TEST, MEANING_TEST)
OLD_VERSION = '|gp-meaning-v3.2'
NEW_VERSION = '|gp-reasons-v3.3'


def prepare(root: Path) -> dict[str, str]:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Run from student-project-management-system repository root.')
    updates = {}
    for rel in PRODUCTION:
        dest, expected, payload = root / rel, HERE / 'expected' / rel, HERE / 'payload' / rel
        if not dest.is_file() or not expected.is_file() or not payload.is_file():
            raise ValueError(f'Missing required file: {rel}')
        if dest.read_text(encoding='utf-8-sig') != expected.read_text(encoding='utf-8-sig'):
            raise ValueError(f'{rel} has local changes or is not v3.2. Refusing overwrite.')
        updates[rel] = payload.read_text(encoding='utf-8-sig')
    for rel in TESTS:
        dest = root / rel
        if not dest.is_file():
            raise ValueError(f'Missing test file: {rel}')
        current = dest.read_text(encoding='utf-8-sig')
        if current.count(OLD_VERSION) != 1 or NEW_VERSION in current:
            raise ValueError(f'{rel}: expected one {OLD_VERSION} assertion; aborting.')
        updates[rel] = current.replace(OLD_VERSION, NEW_VERSION, 1)
    if (root / NEW_TEST).exists():
        raise ValueError(f'Already installed? {NEW_TEST} exists.')
    updates[NEW_TEST] = (HERE / 'payload' / NEW_TEST).read_text(encoding='utf-8-sig')
    return updates


def apply(root: Path, updates: dict[str, str]) -> Path:
    backup = root.parent / ('v011-gp-description-reasons-v33-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in PRODUCTION + TESTS:
        saved = backup / rel
        saved.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, saved)
    edited = []
    try:
        for rel, content in updates.items():
            target = root / rel
            tmp = target.with_name(target.name + '.gp33-tmp')
            tmp.write_text(content, encoding='utf-8')
            tmp.replace(target)
            edited.append(rel)
    except OSError:
        for rel in edited:
            if rel == NEW_TEST:
                (root / rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    updates = prepare(root)
    if args.check:
        print('CHECK OK: v3.2 compatible, 3 services + 2 assertions + 1 new test; no changes.')
    else:
        backup = apply(root, updates)
        print('APPLIED: Git Progress description diagnostics v3.3 (no filter loosening).')
        print('BACKUP:', backup)
        print('NEXT: dotnet build backend; dotnet test backend; restart API; rerun same SHA pair.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        raise SystemExit(1)
