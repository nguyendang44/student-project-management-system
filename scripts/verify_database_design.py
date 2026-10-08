#!/usr/bin/env python3
"""Structural QA for Week 3 database draft, NOT a C# build or SQL migration test."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
entities_code = (ROOT / 'backend/StudentProjects.Domain/Entities/CoreEntities.cs').read_text()
context_code = (ROOT / 'backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs').read_text()
erd_code = (ROOT / 'docs/ERD.md').read_text()
readme_code = (ROOT / 'README.md').read_text()

entities = re.findall(r'public sealed class (\w+)\s*:\s*Entity', entities_code)
dbsets = re.findall(r'public DbSet<(\w+)>\s+\w+\s*=>', context_code)
expected = {
    'User', 'StudentProfile', 'LecturerProfile', 'RegistrationPeriod',
    'LecturerCapacity', 'Topic', 'TopicStateHistory', 'TopicRegistration',
    'LecturerRequest', 'Project', 'Milestone', 'ProgressUpdate',
    'MilestoneSubmission', 'ProjectEvaluation', 'RepositoryLink',
    'CodeAnalysisReport', 'Notification', 'AutomationRun', 'SystemError',
    'AuditEntry', 'SystemSetting',
}
errors = []
if len(entities) != len(set(entities)):
    errors.append('Duplicate entity class names')
if set(entities) != expected:
    errors.append(f'Entity mismatch: {expected ^ set(entities)}')
if set(dbsets) != expected or len(dbsets) != len(expected):
    errors.append(f'DbSet mismatch: {expected ^ set(dbsets)}')

# Determine FK properties per entity body using class boundaries. Also check corresponding FK mappings.
class_positions = list(re.finditer(r'public sealed class (\w+)\s*:\s*Entity\s*\{', entities_code))
fks = []
for idx, match in enumerate(class_positions):
    end = class_positions[idx+1].start() if idx+1 < len(class_positions) else len(entities_code)
    body = entities_code[match.end():end]
    for fk in re.findall(r'public Guid\??\s+(\w+Id)\s*\{', body):
        if fk == 'Id':
            continue
        fks.append((match.group(1), fk))
        # All FK suffix properties require explicit relationship; exceptions are for audit generic EntityId.
        if fk == 'EntityId':
            continue
        pattern = rf'modelBuilder\.Entity<{re.escape(match.group(1))}>\(e\s*=>\s*\{{(.*?)\n\s*\}}\);'
        block_match = re.search(pattern, context_code, re.S)
        if not block_match or not re.search(r'HasForeignKey\(x\s*=>\s*x\.' + re.escape(fk) + r'\)', block_match.group(1)):
            errors.append(f'Missing Fluent API relation: {match.group(1)}.{fk}')

required_constraints = [
    'CK_LecturerCapacity_Bounds', 'CK_Milestone_Percent',
    'CK_ProgressUpdate_Percent', 'CK_Milestone_Dates',
    'CK_RegistrationPeriod_Dates',
]
for c in required_constraints:
    if c not in context_code:
        errors.append(f'Missing check constraint {c}')
for text in ['x.LecturerUserId, x.RegistrationPeriodId', 'x.AcceptedLecturerRequestId', 'x.RowVersion']:
    if text not in context_code:
        errors.append(f'Missing capacity/concurrency/acceptance marker {text}')
if 'Migrations/' in '\n'.join(str(x.relative_to(ROOT)) for x in ROOT.rglob('*') if x.is_file()):
    errors.append('Unexpected migrations created')
if 'Skeleton v0.2' not in readme_code:
    errors.append('README version not updated')

expected_diagram_names = {re.sub(r'(?<!^)(?=[A-Z])', '_', x).upper() for x in expected}
# Class names such as TopicStateHistory become TOPIC_STATE_HISTORY.
mermaid_tables = set(re.findall(r'^\s{2}([A-Z][A-Z_]+)\s*\{\s*$', erd_code, re.M))
if not expected_diagram_names.issubset(mermaid_tables):
    errors.append('ERD missing tables: ' + ', '.join(sorted(expected_diagram_names - mermaid_tables)))
if mermaid_tables - expected_diagram_names:
    errors.append('ERD unknown tables: ' + ', '.join(sorted(mermaid_tables - expected_diagram_names)))

print(f'Entities: {len(entities)}, DbSets: {len(dbsets)}, FK fields: {len(fks)}, ERD tables: {len(mermaid_tables)}, SQL CHECK constraints: {len(required_constraints)}')
if errors:
    print('FAIL:')
    for err in errors:
        print(' -', err)
    raise SystemExit(1)
print('PASS: static mapping/ERD/constraints checks')
print('NOT TESTED: dotnet build, migrations, SQL Server schema creation, concurrent-request behavior')
