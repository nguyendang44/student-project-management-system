from pathlib import Path
import importlib.util
import subprocess
import sys
import pytest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("install35", HERE / "apply_feature_synthesis_v35.py")
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)


def repo(tmp_path):
    root = tmp_path / "student-project-management-system"
    (root / ".git").mkdir(parents=True)
    (root / "backend/StudentProjects.Api").mkdir(parents=True)
    (root / "backend/StudentProjects.Api/Program.cs").write_text("// existing app")
    for rel in installer.SOURCES:
        dest = root / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text((HERE / "expected" / rel).read_text())
    for rel in installer.VERSION_TESTS:
        dest = root / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text('Assert.Contains("' + installer.OLD_KEY + '", client.AnalysisKey);')
    return root


def test_check_is_read_only(tmp_path):
    root = repo(tmp_path)
    result = subprocess.run([sys.executable, str(HERE / "apply_feature_synthesis_v35.py"), str(root), "--check"],
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stderr
    assert "CHECK OK" in result.stdout
    assert not (root / installer.NEW[0]).exists()
    assert not list(tmp_path.glob("v011-gp-v35-backup-*"))


def test_patch_and_backup_and_versions(tmp_path):
    root = repo(tmp_path)
    prepared = installer.prepare(root)
    backup = installer.apply(root, prepared)
    assert backup.is_dir()
    for rel in installer.SOURCES:
        assert (root / rel).read_text() == (HERE / "payload" / rel).read_text()
        assert (backup / rel).read_text() == (HERE / "expected" / rel).read_text()
    for rel in installer.VERSION_TESTS:
        assert installer.NEW_KEY in (root / rel).read_text()
    assert all((root / name).is_file() for name in installer.NEW)
    with pytest.raises(ValueError):
        installer.prepare(root)  # no accidental double-patch


def test_local_edit_is_never_overwritten(tmp_path):
    root = repo(tmp_path)
    rel = installer.SOURCES[0]
    (root / rel).write_text("// local changes")
    with pytest.raises(ValueError, match="not identical"):
        installer.prepare(root)
    assert not list(tmp_path.glob("v011-gp-v35-backup-*"))


def test_missing_version_test_is_rejected(tmp_path):
    root = repo(tmp_path)
    (root / installer.VERSION_TESTS[0]).unlink()
    with pytest.raises(ValueError, match="Missing regression"):
        installer.prepare(root)


def test_wrong_branch_folder_rejected(tmp_path):
    with pytest.raises(ValueError, match="repository root"):
        installer.prepare(tmp_path)


def test_new_file_collision_rejected(tmp_path):
    root = repo(tmp_path)
    dest = root / installer.NEW[0]
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text("// existing feature")
    with pytest.raises(ValueError, match="Refusing"):
        installer.prepare(root)
