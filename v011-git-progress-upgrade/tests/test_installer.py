from pathlib import Path
from tempfile import TemporaryDirectory
import importlib.util
import subprocess
import sys

HERE = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('installer', HERE / 'apply_git_progress.py')
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def fake_v7(root: Path) -> None:
    sources = {
        mod.AI: 'public string Scope => $"sampled-feature-v7-{MaxModuleBatches}";',
        mod.PROGRAM: '''builder.Services.AddHostedService<CodeAnalysisWorker>();
app.MapCodeAnalysisV11Endpoints();''',
        mod.DB: '''using StudentProjects.Domain.Entities;
public DbSet<CodeAnalysisRun> CodeAnalysisRuns => Set<CodeAnalysisRun>();
    public DbSet<CodeAnalysisRun> CodeAnalysisRuns => Set<CodeAnalysisRun>();
        modelBuilder.Entity<CodeAnalysisFinding>(e =>'''.replace(
            'public DbSet<CodeAnalysisRun> CodeAnalysisRuns => Set<CodeAnalysisRun>();\n', '', 1),
        mod.ROUTER: "meta: { title: 'Hồ sơ của tôi', allowedRoles: ['Student', 'Lecturer'] } }, ...moduleRoutes] },",
        mod.VIEW: '      <h2>AI Code Analysis</h2>',
    }
    for rel, source in sources.items():
        path = root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(source, encoding='utf-8')


def test_dry_run_and_exact_new_files():
    with TemporaryDirectory() as directory:
        root = Path(directory)
        fake_v7(root)
        before = (root / mod.PROGRAM).read_text()
        result = mod.prepare(root)
        assert len(result) == 13  # four edits and nine payload files
        process = subprocess.run([sys.executable, str(HERE / 'apply_git_progress.py'), str(root), '--check'],
                                 capture_output=True, text=True)
        assert process.returncode == 0, process.stderr
        assert (root / mod.PROGRAM).read_text() == before
        assert not (root / 'backend/StudentProjects.Domain/Entities/GitProgressRun.cs').exists()


def test_rejects_any_preexisting_target_file():
    with TemporaryDirectory() as directory:
        root = Path(directory)
        fake_v7(root)
        target = root / 'backend/StudentProjects.Domain/Entities/GitProgressRun.cs'
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text('already exists')
        try:
            mod.prepare(root)
            assert False, 'expected installer to reject overwrite'
        except ValueError as exc:
            assert 'overwrite' in str(exc)


def test_rejects_incompatible_existing_version():
    with TemporaryDirectory() as directory:
        root = Path(directory)
        fake_v7(root)
        (root / mod.AI).write_text('sampled-feature-v6-4')
        try:
            mod.prepare(root)
            assert False
        except ValueError as exc:
            assert 'v7' in str(exc)


def test_all_payload_is_utf8_and_no_file_names_collide():
    paths = [p.relative_to(mod.PAYLOAD).as_posix() for p in mod.PAYLOAD.rglob('*') if p.is_file()]
    assert len(paths) == len(set(paths))
    assert len(paths) == 9
    for path in paths:
        (mod.PAYLOAD / path).read_text(encoding='utf-8')


def test_apply_creates_new_files_and_refuses_second_install():
    with TemporaryDirectory() as directory:
        root = Path(directory) / 'repository'
        root.mkdir()
        fake_v7(root)
        proc = subprocess.run([sys.executable, str(HERE / 'apply_git_progress.py'), str(root)],
                              capture_output=True, text=True)
        assert proc.returncode == 0, proc.stderr
        assert 'GitProgressWorker' in (root / mod.PROGRAM).read_text()
        assert 'GitProgressRuns' in (root / mod.DB).read_text()
        assert 'GitProgressV11View.vue' in (root / mod.ROUTER).read_text()
        assert (root / 'backend/StudentProjects.Domain/Entities/GitProgressRun.cs').is_file()
        second = subprocess.run([sys.executable, str(HERE / 'apply_git_progress.py'), str(root), '--check'],
                                capture_output=True, text=True)
        assert second.returncode != 0
        assert 'already appears installed' in second.stderr
