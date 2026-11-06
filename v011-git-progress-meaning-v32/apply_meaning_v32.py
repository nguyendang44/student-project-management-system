#!/usr/bin/env python3
"""Upgrade Git Progress v3.1 to v3.2 without overwriting manually fixed tests."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
POLICY = 'backend/StudentProjects.Api/Services/GitProgressMeaningPolicy.cs'
CLIENT = 'backend/StudentProjects.Api/Services/AiGitProgressClient.cs'
MEANING_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs'
EVIDENCE_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs'
NEW_TEST = 'backend/StudentProjects.IntegrationTests/GitProgressDescriptionV32Tests.cs'
PRODUCTION = (POLICY, CLIENT)
UPDATE_TESTS = (MEANING_TEST, EVIDENCE_TEST)
NEW_VERSION = '|gp-meaning-v3.2'
OLD_VERSIONS = ('|gp-diagnostics-v3.1', '|gp-meaningful-v3')


def rewrite_test(text: str, rel: str) -> str:
    # Only change the cached-analysis version assertion. Keep all human edits,
    # including fixes to the malformed C# literals in v3.1 test files.
    hits = [(old, text.count(old)) for old in OLD_VERSIONS]
    if sum(count for _, count in hits) != 1:
        raise ValueError(f'{rel}: expected exactly one v3/v3.1 analysis version assertion.')
    old = next(old for old, n in hits if n == 1)
    return text.replace(old, NEW_VERSION, 1)


def prepare(root: Path) -> dict[str, str]:
    if not (root / '.git').is_dir() or not (root / 'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Run from the student-project-management-system repository root.')
    result: dict[str, str] = {}
    for rel in PRODUCTION:
        target = root / rel
        expected = HERE / 'expected' / rel
        replacement = HERE / 'payload' / rel
        if not target.is_file() or not expected.is_file() or not replacement.is_file():
            raise ValueError(f'Missing required file: {rel}')
        current = target.read_text(encoding='utf-8-sig')
        if current != expected.read_text(encoding='utf-8-sig'):
            raise ValueError(f'{rel}: does not match v3.1. Refusing to overwrite local edits.')
        result[rel] = replacement.read_text(encoding='utf-8-sig')
    for rel in UPDATE_TESTS:
        target = root / rel
        if not target.is_file():
            raise ValueError(f'Missing existing test file: {rel}')
        result[rel] = rewrite_test(target.read_text(encoding='utf-8-sig'), rel)
    if (root / NEW_TEST).exists():
        raise ValueError(f'New v3.2 test already exists: {NEW_TEST}')
    result[NEW_TEST] = (HERE / 'payload' / NEW_TEST).read_text(encoding='utf-8-sig')
    return result


def apply(root: Path, updates: dict[str, str]) -> Path:
    backup = root.parent / ('v011-gp-meaning-v32-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in PRODUCTION + UPDATE_TESTS:
        saved = backup / rel
        saved.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, saved)
    edited = []
    try:
        for rel, content in updates.items():
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            temp = target.with_name(target.name + '.gp32-tmp')
            temp.write_text(content, encoding='utf-8')
            temp.replace(target)
            edited.append(rel)
    except OSError:
        for rel in edited:
            if rel == NEW_TEST:
                (root / rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Repository root')
    parser.add_argument('--check', action='store_true', help='Validate v3.1 compatibility without changes')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    updates = prepare(root)
    if args.check:
        print('CHECK OK: v3.1 compatible; 2 C# service files + 2 version assertions + 1 new test; no edits.')
        return
    backup = apply(root, updates)
    print('APPLIED: Git Progress meaningful descriptions v3.2')
    print('BACKUP:', backup)
    print('NEXT: dotnet build backend; dotnet test backend; restart API; re-analyze the same SHA pair.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError) as error:
        print('ERROR:', error, file=sys.stderr)
        raise SystemExit(1)
