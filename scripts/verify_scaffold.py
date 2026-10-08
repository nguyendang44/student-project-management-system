"""Run from any directory: python scripts/verify_scaffold.py. Standard library only."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
api = (ROOT / 'backend/StudentProjects.Api/Endpoints/SkeletonEndpoints.cs').read_text(encoding='utf-8')
modules = (ROOT / 'frontend/src/app/modules.ts').read_text(encoding='utf-8')
entities = (ROOT / 'backend/StudentProjects.Domain/Entities/CoreEntities.cs').read_text(encoding='utf-8')
rows = re.findall(r'api\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"', api)
routes = set(rows)
all_ucs = set(re.findall(r'UC-(\d{2})', api))
check = {
  '43 use cases covered': all_ucs == {f'{i:02}' for i in range(1,44)},
  'no duplicate METHOD PATH': len(routes) == len(rows),
  '23 frontend module routes': modules.count('"id":') == 23,
  '23 frontend feature API clients': len(list((ROOT/'frontend/src/features').glob('*/*.api.ts'))) == 24,  # 23 modules + auth
  'backend 4 projects': len(list((ROOT/'backend').glob('*/*.csproj'))) == 4,
  '20 domain entities': len(re.findall(r'public sealed class \w+ : Entity', entities)) == 20,
  'all endpoints are pending': api.count('Pending(') == len(rows) + 1, # route handlers + Pending helper
  'lecturer-only milestone approval': '"/milestones/{id:guid}/approve", () => Pending("milestones", "UC-25")).RequireAuthorization(policy => policy.RequireRole("Lecturer"))' in api,
  'lecturer-only accept': '"/lecturer-requests/{id:guid}/accept", () => Pending("lecturerrequests", "UC-11,UC-12,UC-13,UC-14")).RequireAuthorization(policy => policy.RequireRole("Lecturer"))' in api,
  'student-only submit': '"/submissions", () => Pending("submissions", "UC-23,UC-24,UC-25")).RequireAuthorization(policy => policy.RequireRole("Student"))' in api,
  'no accidental secrets': not any((ROOT / f).exists() for f in ('frontend/.env','backend/.env')),
}
for label, success in check.items():
    print(f'{"PASS" if success else "FAIL"}: {label}')
print(f'API route declarations: {len(rows)} | UC IDs covered: {len(all_ucs)} | Entities: {len(re.findall(r"public sealed class \w+ : Entity", entities))}')
if not all(check.values()): sys.exit(1)
