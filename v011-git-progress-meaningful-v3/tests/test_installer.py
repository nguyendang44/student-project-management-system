from pathlib import Path
import importlib.util
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parents[1]
SCRIPT = HERE / 'apply_progress_meaningful_v3.py'
spec = importlib.util.spec_from_file_location('gpv3_installer', SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def repo(tmp_path: Path) -> Path:
    root = tmp_path / 'repo'
    (root / '.git').mkdir(parents=True)
    (root / 'backend/StudentProjects.Api').mkdir(parents=True)
    (root / 'backend/StudentProjects.Api/Program.cs').write_text('// app', encoding='utf-8')
    for rel in mod.EDITED:
        target = root / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(HERE / 'expected' / rel, target)
    return root


def test_check_does_not_mutate_project(tmp_path):
    root = repo(tmp_path)
    before = {r: (root / r).read_bytes() for r in mod.EDITED}
    result = subprocess.run([sys.executable, str(SCRIPT), str(root), '--check'],
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stderr
    assert 'CHECK OK' in result.stdout
    assert all((root / r).read_bytes() == old for r, old in before.items())
    assert all(not (root / r).exists() for r in mod.ADDED)


def test_apply_creates_backup_and_updates_only_expected_files(tmp_path):
    root = repo(tmp_path)
    backup = mod.apply(root)
    for rel in mod.EDITED:
        assert (backup / rel).read_bytes() == (HERE / 'expected' / rel).read_bytes()
        assert (root / rel).read_bytes() == (HERE / 'payload' / rel).read_bytes()
    for rel in mod.ADDED:
        assert (root / rel).read_bytes() == (HERE / 'payload' / rel).read_bytes()
    assert (root / 'backend/StudentProjects.Api/Program.cs').read_text() == '// app'


def test_full_apply_and_second_apply_rejected(tmp_path):
    root = repo(tmp_path)
    output = subprocess.run([sys.executable, str(SCRIPT), str(root)],
                            capture_output=True, text=True)
    assert output.returncode == 0, output.stderr
    repeated = subprocess.run([sys.executable, str(SCRIPT), str(root), '--check'],
                              capture_output=True, text=True)
    assert repeated.returncode != 0
    assert 'already applied' in repeated.stderr


def test_refuses_local_changes_without_touching_other_files(tmp_path):
    root = repo(tmp_path)
    first = root / mod.EDITED[0]
    first.write_text(first.read_text() + '// custom change\n', encoding='utf-8')
    before = {r: (root / r).read_bytes() for r in mod.EDITED}
    bad = subprocess.run([sys.executable, str(SCRIPT), str(root), '--check'],
                         capture_output=True, text=True)
    assert bad.returncode != 0
    assert 'refusing overwrite' in bad.stderr
    assert all((root / r).read_bytes() == old for r, old in before.items())


def test_rejects_conflicting_new_test_file(tmp_path):
    root = repo(tmp_path)
    (root / mod.ADDED[0]).write_text('// conflict', encoding='utf-8')
    result = subprocess.run([sys.executable, str(SCRIPT), str(root), '--check'],
                            capture_output=True, text=True)
    assert result.returncode != 0
    assert 'already exists' in result.stderr
