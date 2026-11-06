#!/usr/bin/env python3
"""Safe, one-time Git Progress AI Explainer v3.6 -> v3.7 upgrade."""
from __future__ import annotations
import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
MODIFIED = [
    'backend/StudentProjects.Api/Services/GitProgressAiExplanationPolicy.cs',
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'frontend/src/features/gitProgress/gitProgress.api.ts',
    'frontend/src/views/GitProgressV11View.vue',
]
NEW = [
    'backend/StudentProjects.Api/Services/GitProgressFeatureReport.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressAiExplanationV37Tests.cs',
]
VERSION_TESTS = [
    'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressGroundedFallbackV34Tests.cs',
]
OLD = '|gp-explain-v3.6'
NEW_KEY = '|gp-purpose-v3.7'


def prepare(root: Path) -> dict[str, str]:
    if not (root/'.git').is_dir() or not (root/'backend/StudentProjects.Api/Program.cs').is_file():
        raise ValueError('Chạy trong thư mục gốc student-project-management-system.')
    updated: dict[str, str] = {}
    for rel in MODIFIED:
        target = root/rel
        expected = HERE/'expected'/rel
        payload = HERE/'payload'/rel
        if not all(p.is_file() for p in (target, expected, payload)):
            raise ValueError(f'Thiếu file đầu vào: {rel}')
        if target.read_text(encoding='utf-8-sig') != expected.read_text(encoding='utf-8-sig'):
            raise ValueError(f'{rel}: khác bản v3.6 gốc; KHÔNG ghi đè thay đổi của bạn.')
        updated[rel] = payload.read_text(encoding='utf-8-sig')
    for rel in NEW:
        target = root/rel
        if target.exists():
            raise ValueError(f'File đã tồn tại: {rel}; không ghi đè.')
        payload = HERE/'payload'/rel
        if not payload.is_file():
            raise ValueError(f'Thiếu payload {rel}')
        updated[rel] = payload.read_text(encoding='utf-8-sig')
    for rel in VERSION_TESTS:
        path = root/rel
        if not path.is_file():
            raise ValueError(f'Thiếu regression test: {rel}')
        content = path.read_text(encoding='utf-8-sig')
        if content.count(OLD) != 1 or NEW_KEY in content:
            raise ValueError(f'{rel}: không thấy đúng một assertion phiên bản {OLD}')
        updated[rel] = content.replace(OLD, NEW_KEY, 1)
    if not (root/'backend/StudentProjects.IntegrationTests/GitProgressAiExplanationV36Tests.cs').is_file():
        raise ValueError('Thiếu test v3.6; hãy xác nhận đã áp dụng v3.6 trước.')
    return updated


def install(root: Path, updated: dict[str, str]) -> Path:
    backup = root.parent/('v011-gp-v37-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for rel in MODIFIED + VERSION_TESTS:
        out = backup/rel
        out.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root/rel, out)
    changed: list[str] = []
    try:
        for rel, content in updated.items():
            path = root/rel
            path.parent.mkdir(parents=True, exist_ok=True)
            tmp = path.with_name(path.name + '.v37-tmp')
            tmp.write_text(content, encoding='utf-8')
            tmp.replace(path)
            changed.append(rel)
    except OSError:
        for rel in reversed(changed):
            path = root/rel
            if rel in NEW:
                path.unlink(missing_ok=True)
            else:
                shutil.copy2(backup/rel, path)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Thư mục gốc dự án')
    parser.add_argument('--check', action='store_true', help='Kiểm tra tương thích, không ghi file')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    ready = prepare(root)
    if args.check:
        print(f'CHECK OK: v3.6 compatible; validated {len(ready)} files; no changes written.')
    else:
        backup = install(root, ready)
        print('APPLIED: Git Progress AI Purpose v3.7')
        print('BACKUP:', backup)
        print('NEXT: dotnet build backend; dotnet test backend; cd frontend; npm run build')

if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        sys.exit(1)
