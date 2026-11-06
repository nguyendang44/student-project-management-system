import importlib.util
from pathlib import Path
import shutil
import tempfile
import pytest

HERE = Path(__file__).parent
spec = importlib.util.spec_from_file_location('gp_diag_installer', HERE / 'apply_progress_diagnostics.py')
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def setup_repo(tmp_path):
    repo = tmp_path / 'repo'
    (repo / '.git').mkdir(parents=True)
    (repo / 'backend/StudentProjects.Api').mkdir(parents=True)
    (repo / 'backend/StudentProjects.Api/Program.cs').write_text('// sample')
    for rel in mod.FILES:
        path = repo / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(HERE / 'expected' / rel, path)
    return repo


def test_check_does_not_modify_baseline(tmp_path):
    repo = setup_repo(tmp_path)
    before = {(repo / rel).read_bytes() for rel in mod.FILES}
    mod.prepare(repo)
    after = {(repo / rel).read_bytes() for rel in mod.FILES}
    assert before == after


def test_install_and_backup(tmp_path):
    repo = setup_repo(tmp_path)
    mod.prepare(repo)
    backup = mod.apply(repo)
    assert backup.is_dir()
    for rel in mod.FILES:
        assert (repo / rel).read_bytes() == (HERE / 'payload' / rel).read_bytes()
        assert (backup / rel).read_bytes() == (HERE / 'expected' / rel).read_bytes()


def test_reapplication_rejected(tmp_path):
    repo = setup_repo(tmp_path)
    mod.apply(repo)
    with pytest.raises(ValueError, match='already applied'):
        mod.prepare(repo)


def test_modified_source_rejected(tmp_path):
    repo = setup_repo(tmp_path)
    target = repo / mod.FILES[0]
    target.write_text('changed')
    with pytest.raises(ValueError, match='incompatible v3'):
        mod.prepare(repo)


def test_no_repo_rejected(tmp_path):
    with pytest.raises(ValueError, match='repository root'):
        mod.prepare(tmp_path)
