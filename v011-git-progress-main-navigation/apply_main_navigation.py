#!/usr/bin/env python3
"""Register existing Git Progress v3.5 as a first-class sidebar module.

Only edits three frontend files; never touches backend, SQL, or Git history.
Usage:
  py .\v011-git-progress-main-navigation\apply_main_navigation.py . --check
  py .\v011-git-progress-main-navigation\apply_main_navigation.py .
"""
from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path
import re
import shutil
import sys

MODULES = Path('frontend/src/app/modules.ts')
ROUTER = Path('frontend/src/app/router.ts')
DASHBOARD = Path('frontend/src/views/DashboardV08View.vue')
LAYOUT = Path('frontend/src/components/AppLayout.vue')
VIEW = Path('frontend/src/views/GitProgressV11View.vue')
FILES = [MODULES, ROUTER, DASHBOARD]

MODULE_ENTRY = '''  {
    "id": "gitprogress",
    "path": "/git-progress",
    "group": "Tích hợp",
    "title": "Phân tích tiến độ Git",
    "description": "So sánh hai commit và tổng hợp công việc có bằng chứng từ Git diff",
    "roles": ["Student", "Lecturer", "Admin"],
    "requirements": "v0.11 Git Progress Analysis",
    "useCases": [],
    "columns": ["Dự án", "Commit cũ", "Commit mới", "Báo cáo"],
    "endpoints": [
      "POST /api/v1/v11/repositories/{id}/progress",
      "GET /api/v1/v11/repositories/{id}/progress",
      "GET /api/v1/v11/progress/{id}"
    ]
  },
'''

OLD_ROUTE = re.compile(
    r"\n[ \t]*\{[ \t]*path:[ \t]*'git-progress'[ \t]*,[ \t]*"
    r"component:[ \t]*\(\)[ \t]*=>[ \t]*import\('\.\./views/GitProgressV11View\.vue'\)[ \t]*,\s*"
    r"meta:[ \t]*\{[ \t]*title:[ \t]*'Git Progress Analysis'[ \t]*,[ \t]*"
    r"allowedRoles:[ \t]*\['Student',[ \t]*'Lecturer',[ \t]*'Admin'\][ \t]*\}[ \t]*\},"
)


def replace_once(value: str, old: str, new: str, label: str) -> str:
    count = value.count(old)
    if count != 1:
        raise ValueError(f'{label}: expected exactly one compatible anchor; found {count}. No files changed.')
    return value.replace(old, new, 1)


def prepare(root: Path) -> dict[Path, tuple[str, str]]:
    if not (root / '.git').is_dir():
        raise ValueError('Run from the repository root (folder containing .git).')
    for path in FILES + [LAYOUT, VIEW]:
        if not (root / path).is_file():
            raise ValueError(f'Missing required file: {path}. No files changed.')
    sources = {path: (root / path).read_text(encoding='utf-8-sig') for path in FILES}
    modules, router, dashboard = [sources[p] for p in FILES]
    layout = (root / LAYOUT).read_text(encoding='utf-8-sig')
    if 'modules.filter(' not in layout or 'group.items' not in layout:
        raise ValueError('AppLayout sidebar no longer uses modules.ts; refusing an unsafe menu edit.')
    if 'GitProgressV11View.vue' not in router:
        raise ValueError('Git Progress route is missing; install the Git Progress feature first.')
    if "| 'gitprogress'" in modules or '"id": "gitprogress"' in modules:
        raise ValueError('Git Progress is already registered as a main menu module. No files changed.')

    # AppLayout reads modules.ts; registering a module adds it to the main sidebar.
    modules = replace_once(modules, "  | 'ai'\n", "  | 'ai'\n  | 'gitprogress'\n", 'ModuleId type')
    modules = replace_once(modules,
        '  {\n    "id": "notifications",',
        MODULE_ENTRY + '  {\n    "id": "notifications",',
        'Module sidebar item position')
    router = replace_once(router,
        "  : mod.id === 'github' ? () => import('../views/GitHubV10View.vue')",
        "  : mod.id === 'gitprogress' ? () => import('../views/GitProgressV11View.vue')\n"
        "  : mod.id === 'github' ? () => import('../views/GitHubV10View.vue')",
        'Dynamic Vue route component')

    # Remove the previous manual child-route to avoid two records for /git-progress.
    router, count = OLD_ROUTE.subn('', router)
    if count != 1:
        raise ValueError(f'Manual /git-progress route: expected exactly one v0.11 entry, found {count}. No files changed.')
    if router.count("'git-progress'") != 0:
        raise ValueError('Unexpected duplicate git-progress route; refusing to modify.')
    dashboard = replace_once(dashboard,
        '<RouterLink to="/guidance">Nhật ký hướng dẫn</RouterLink></p>',
        '<RouterLink to="/guidance">Nhật ký hướng dẫn</RouterLink> · '
        '<RouterLink to="/git-progress">Phân tích tiến độ Git</RouterLink></p>',
        'Dashboard quick link')
    return {
        MODULES: (sources[MODULES], modules),
        ROUTER: (sources[ROUTER], router),
        DASHBOARD: (sources[DASHBOARD], dashboard),
    }


def apply(root: Path, changes: dict[Path, tuple[str, str]]) -> Path:
    backup = root.parent / ('v011-gp-nav-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    backup.mkdir(parents=True, exist_ok=False)
    for relative in changes:
        dest = backup / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / relative, dest)
    written: list[Path] = []
    try:
        for relative, (_, updated) in changes.items():
            dest = root / relative
            temp = dest.with_name(dest.name + '.gp-nav-tmp')
            try:
                temp.write_text(updated, encoding='utf-8')
                temp.replace(dest)
            finally:
                if temp.exists():
                    temp.unlink()
            written.append(relative)
    except Exception:
        for relative in written:
            shutil.copy2(backup / relative, root / relative)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('repository', help='Root folder of student-project-management-system')
    parser.add_argument('--check', action='store_true', help='Validate only; do not modify files')
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    changes = prepare(root)
    print('Compatible with Git Progress frontend v0.11. Files to change:')
    for relative in changes:
        print('  ~', relative)
    if args.check:
        print('CHECK OK: no files modified.')
        return
    backup = apply(root, changes)
    print('APPLIED: Git Progress added to the main sidebar and Dashboard.')
    print('BACKUP:', backup)
    print('NEXT: cd frontend; npm run build; npm run dev')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print('ERROR:', exc, file=sys.stderr)
        raise SystemExit(1)
