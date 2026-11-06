#!/usr/bin/env python3
"""Safely upgrade the v0.11 Git Progress code to evidence-ID citations (v2).

Run from repository root:
  py .\v011-git-progress-evidence-ids\apply_progress_evidence_ids.py . --check
  py .\v011-git-progress-evidence-ids\apply_progress_evidence_ids.py .
"""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE=Path(__file__).resolve().parent
EXPECTED=HERE/'expected'
PAYLOAD=HERE/'payload'
EDITED=(
 'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
 'backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs',
 'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
 'backend/StudentProjects.Api/Endpoints/GitProgressV11Endpoints.cs',
)
ADDED=('backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',)


def prepare(root: Path) -> None:
    if not (root/'backend'/'StudentProjects.Api'/'Program.cs').is_file():
        raise ValueError('Not a student-project-management-system repository')
    for rel in EDITED:
        target=root/rel
        if not target.is_file():
            raise ValueError(f'Missing expected v0.11 file: {rel}')
        before=target.read_text(encoding='utf-8-sig')
        expected=(EXPECTED/rel).read_text(encoding='utf-8-sig')
        after=(PAYLOAD/rel).read_text(encoding='utf-8-sig')
        if before != expected:
            raise ValueError(f'File differs from Git Progress v1: {rel}. Not overwriting local changes.')
        if before == after:
            raise ValueError(f'Patch already appears applied: {rel}')
    for rel in ADDED:
        if (root/rel).exists():
            raise ValueError(f'New test file already exists: {rel}')
        if not (PAYLOAD/rel).is_file():
            raise ValueError(f'Missing patch payload: {rel}')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    root=Path(args.repository).expanduser().resolve()
    prepare(root)
    print(f'Validated: {len(EDITED)} edits + {len(ADDED)} regression test file; no migrations needed.')
    if args.check:
        print('CHECK OK: source files were not changed.')
        return
    backup=root.parent/('v011-git-progress-evidence-backup-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True,exist_ok=False)
    changed=[]
    try:
        for rel in EDITED:
            prev=root/rel
            dst=backup/rel
            dst.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(prev,dst)
        for rel in EDITED+ADDED:
            target=root/rel
            target.parent.mkdir(parents=True,exist_ok=True)
            tmp=target.with_name(target.name+'.gp-evidence-tmp')
            tmp.write_text((PAYLOAD/rel).read_text(encoding='utf-8-sig'),encoding='utf-8')
            tmp.replace(target)
            changed.append(rel)
    except OSError:
        for rel in changed:
            if rel in ADDED:
                (root/rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup/rel,root/rel)
        raise
    print('Applied evidence-ID v2. Backup saved outside repository:',backup)
    print('NEXT: dotnet build backend; dotnet test backend; restart API; rerun same Git commit pair.')
    print('No SQL migration. Old reports are retained; analysis key is versioned to avoid cached results.')


if __name__=='__main__':
    try: main()
    except (OSError,ValueError) as exc:
        print('ERROR:',exc,file=sys.stderr)
        raise SystemExit(1)
