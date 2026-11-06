from pathlib import Path
import subprocess
import sys
import tempfile
import shutil

HERE = Path(__file__).resolve().parent
INSTALLER = HERE / 'apply_grounded_v34.py'
SOURCES = [
    'backend/StudentProjects.Api/Services/AiGitProgressClient.cs',
    'backend/StudentProjects.Api/Services/GitProgressWorker.cs',
    'backend/StudentProjects.Api/Services/GitProgressEvidencePolicy.cs',
    'frontend/src/views/GitProgressV11View.vue',
    'frontend/src/features/gitProgress/gitProgress.api.ts',
]
TESTS = [
    'backend/StudentProjects.IntegrationTests/GitProgressEvidenceIdTests.cs',
    'backend/StudentProjects.IntegrationTests/GitProgressMeaningfulTests.cs',
]


def fixture(repo: Path):
    (repo / '.git').mkdir(parents=True)
    p = repo / 'backend/StudentProjects.Api/Program.cs'
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text('// fixture')
    for relative in SOURCES:
        dest = repo / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text((HERE/'expected'/relative).read_text())
    for relative in TESTS:
        dest = repo / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text('Assert.Contains("|gp-reasons-v3.3", client.AnalysisKey);')


def run(repo, *args):
    return subprocess.run([sys.executable, str(INSTALLER), str(repo), *args],
                          capture_output=True, text=True)


def test_check_no_mutation(tmp_path):
    repo = tmp_path / 'repo'; fixture(repo)
    before = (repo / SOURCES[0]).read_bytes()
    result = run(repo, '--check')
    assert result.returncode == 0, result.stderr
    assert 'CHECK OK' in result.stdout
    assert (repo / SOURCES[0]).read_bytes() == before
    assert not (repo/'backend/StudentProjects.Api/Services/GitProgressGroundedFallback.cs').exists()


def test_apply_and_versions(tmp_path):
    repo=tmp_path/'repo'; fixture(repo)
    result=run(repo)
    assert result.returncode == 0, result.stderr
    assert 'APPLIED' in result.stdout
    assert 'gp-grounded-v3.4' in (repo / SOURCES[0]).read_text()
    assert 'ruleBasedFallback=' in (repo / SOURCES[1]).read_text()
    assert 'diff-rule' in (repo / SOURCES[1]).read_text()
    assert (repo / 'backend/StudentProjects.Api/Services/GitProgressGroundedFallback.cs').is_file()
    assert (repo / 'backend/StudentProjects.IntegrationTests/GitProgressGroundedFallbackV34Tests.cs').is_file()
    assert all('gp-grounded-v3.4' in (repo/rel).read_text() for rel in TESTS)


def test_refuses_reapply(tmp_path):
    repo=tmp_path/'repo'; fixture(repo)
    assert run(repo).returncode == 0
    assert run(repo).returncode != 0


def test_refuses_custom_edits(tmp_path):
    repo=tmp_path/'repo'; fixture(repo)
    (repo / SOURCES[0]).write_text('user change')
    result=run(repo)
    assert result.returncode != 0
    assert (repo / SOURCES[0]).read_text() == 'user change'


def test_refuses_bad_test_version(tmp_path):
    repo=tmp_path/'repo'; fixture(repo)
    (repo / TESTS[0]).write_text('Assert.Contains("|gp-meaningful-v3", client.AnalysisKey);')
    assert run(repo).returncode != 0


def test_verified_rule_is_present():
    source=(HERE/'payload/backend/StudentProjects.Api/Services/GitProgressGroundedFallback.cs').read_text()
    assert 'GitProgressEvidencePolicy.EvidenceLines(g.Single())' in source
    assert '!changed.Contains(reference.Evidence)' in source
    assert '!GitProgressMeaningPolicy.IsFunctionalLine' in source
    assert 'if (!evidence.StartsWith(\'+\')' in source
