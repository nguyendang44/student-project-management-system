from pathlib import Path
import importlib.util
import tempfile
import shutil
import subprocess
import sys

HERE=Path(__file__).parent
spec=importlib.util.spec_from_file_location('installer', HERE/'apply_meaning_v32.py')
ins=importlib.util.module_from_spec(spec)
spec.loader.exec_module(ins)


def fixture(root):
    (root/'.git').mkdir(parents=True)
    prog=root/'backend/StudentProjects.Api/Program.cs'; prog.parent.mkdir(parents=True);prog.write_text('// test')
    for rel in ins.PRODUCTION:
        t=root/rel;t.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(HERE/'expected'/rel,t)
    for rel in ins.UPDATE_TESTS:
        t=root/rel;t.parent.mkdir(parents=True,exist_ok=True)
        t.write_text('test source edited manually: +api.MapPost("/x", Handler);\nAssert.Contains("'+ins.OLD_VERSIONS[0]+'", key);')


def test_round_trip():
    with tempfile.TemporaryDirectory() as tmp:
        root=Path(tmp)/'repo';root.mkdir();fixture(root)
        before=(root/ins.MEANING_TEST).read_text()
        updates=ins.prepare(root)
        assert (root/ins.CLIENT).read_text() == (HERE/'expected'/ins.CLIENT).read_text()
        assert 'gp-meaning-v3.2' in updates[ins.MEANING_TEST]
        backup=ins.apply(root, updates)
        assert 'gp-meaning-v3.2' in (root/ins.CLIENT).read_text()
        assert 'test source edited manually' in (root/ins.MEANING_TEST).read_text()
        assert (backup/ins.MEANING_TEST).read_text()==before
        assert (root/ins.NEW_TEST).is_file()
        try: ins.prepare(root)
        except ValueError: pass
        else: raise AssertionError('re-applying must refuse')


def test_baseline_guard():
    with tempfile.TemporaryDirectory() as tmp:
        root=Path(tmp)/'repo';root.mkdir();fixture(root)
        p=root/ins.POLICY;p.write_text(p.read_text()+'// user change')
        try: ins.prepare(root)
        except ValueError: pass
        else: raise AssertionError('must refuse overwriting user edits')


def test_check_is_read_only():
    with tempfile.TemporaryDirectory() as tmp:
        root=Path(tmp)/'repo';root.mkdir();fixture(root)
        result=subprocess.run([sys.executable,str(HERE/'apply_meaning_v32.py'),str(root),'--check'],capture_output=True,text=True)
        assert result.returncode==0, result.stderr
        assert 'CHECK OK' in result.stdout
        assert not (root/ins.NEW_TEST).exists()


def test_source_guards_and_prompt():
    policy=(HERE/'payload'/ins.POLICY).read_text()
    client=(HERE/'payload'/ins.CLIENT).read_text()
    assert 'UnsupportedClaim.IsMatch(normalized)' in policy
    assert 'EmptyObservation.IsMatch(normalized)' in policy
    assert 'AdditionalAction.IsMatch(normalized)' in policy
    assert 'changed.Contains(reference.Evidence)' in policy
    assert client.count('gp-meaning-v3.2')==1
    assert 'Begin each description with one of these exact verbs' in client


def test_test_source_string_escaping():
    t=(HERE/'payload'/ins.NEW_TEST).read_text()
    assert 'MapPost(\\"/progress\\", Start)' in t
    assert 'exposes a POST' in t
