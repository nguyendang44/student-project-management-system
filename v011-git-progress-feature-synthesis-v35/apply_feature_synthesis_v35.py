#!/usr/bin/env python3
"""Upgrade a clean v3.4 Git Progress install to evidence-grouped v3.5."""
from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
SOURCES = [
    "backend/StudentProjects.Api/Services/AiGitProgressClient.cs",
    "backend/StudentProjects.Api/Services/GitProgressWorker.cs",
    "backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs",
    "frontend/src/features/gitProgress/gitProgress.api.ts",
    "frontend/src/views/GitProgressV11View.vue",
]
NEW = [
    "backend/StudentProjects.Api/Services/GitProgressFeatureSynthesis.cs",
    "backend/StudentProjects.IntegrationTests/GitProgressFeatureSynthesisV35Tests.cs",
]
VERSION_TESTS = [
    "backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs",
    "backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs",
    "backend/StudentProjects.IntegrationTests/GitProgressGroundedFallbackV34Tests.cs",
]
OLD_KEY = "|gp-grounded-v3.4"
NEW_KEY = "|gp-features-v3.5"


def prepare(root: Path) -> dict[str, str]:
    if not (root / ".git").is_dir() or not (root / "backend/StudentProjects.Api/Program.cs").is_file():
        raise ValueError("Run from the student-project-management-system repository root.")
    prepared: dict[str, str] = {}
    for rel in SOURCES:
        dest = root / rel
        before = HERE / "expected" / rel
        after = HERE / "payload" / rel
        if not all(p.is_file() for p in (dest, before, after)):
            raise ValueError(f"Missing source/payload: {rel}")
        if dest.read_text(encoding="utf-8-sig") != before.read_text(encoding="utf-8-sig"):
            raise ValueError(f"{rel}: not identical to v3.4; refusing to overwrite local edits")
        prepared[rel] = after.read_text(encoding="utf-8-sig")
    for rel in VERSION_TESTS:
        dest = root / rel
        if not dest.is_file():
            raise ValueError(f"Missing regression test: {rel}")
        source = dest.read_text(encoding="utf-8-sig")
        if source.count(OLD_KEY) != 1 or NEW_KEY in source:
            raise ValueError(f"{rel}: expected exactly one {OLD_KEY} version assertion")
        prepared[rel] = source.replace(OLD_KEY, NEW_KEY, 1)
    for rel in NEW:
        if (root / rel).exists():
            raise ValueError(f"Refusing to overwrite existing file: {rel}")
        source = HERE / "payload" / rel
        if not source.is_file():
            raise ValueError(f"Missing new payload: {rel}")
        prepared[rel] = source.read_text(encoding="utf-8-sig")
    return prepared


def apply(root: Path, prepared: dict[str, str]) -> Path:
    backup = root.parent / ("v011-gp-v35-backup-" + datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    backup.mkdir(parents=True)
    for rel in SOURCES + VERSION_TESTS:
        dest = backup / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / rel, dest)
    changed: list[str] = []
    try:
        for rel, content in prepared.items():
            dest = root / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            tmp = dest.with_name(dest.name + ".v35-tmp")
            tmp.write_text(content, encoding="utf-8")
            tmp.replace(dest)
            changed.append(rel)
    except OSError:
        for rel in reversed(changed):
            if rel in NEW:
                (root / rel).unlink(missing_ok=True)
            else:
                shutil.copy2(backup / rel, root / rel)
        raise
    return backup


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repository", help="Root folder of student-project-management-system")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(args.repository).expanduser().resolve()
    data = prepare(root)
    if args.check:
        print(f"CHECK OK: compatible with v3.4; {len(data)} files prepared; no writes.")
    else:
        backup = apply(root, data)
        print("APPLIED: Git Progress v3.5 grouped feature synthesis.")
        print("BACKUP:", backup)
        print("NEXT: dotnet build backend; dotnet test backend; npm run build (frontend)")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as exc:
        print("ERROR:", exc, file=sys.stderr)
        raise SystemExit(1)
