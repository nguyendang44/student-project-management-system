#!/usr/bin/env python3
"""Run locally with py test_installer.py (requires only Python stdlib)."""
import tempfile
import unittest
from pathlib import Path
from apply_ai_explainer_v36 import HERE, MODIFIED, NEW, VERSION_TESTS, OLD, NEW_KEY, prepare, install

class InstallerTests(unittest.TestCase):
    def make_fixture(self, root):
        (root/'.git').mkdir()
        p=root/'backend/StudentProjects.Api/Program.cs'
        p.parent.mkdir(parents=True,exist_ok=True)
        p.write_text('dummy app')
        for rel in MODIFIED:
            p=root/rel; p.parent.mkdir(parents=True,exist_ok=True)
            p.write_text((HERE/'expected'/rel).read_text(encoding='utf-8'))
        for rel in VERSION_TESTS:
            p=root/rel; p.parent.mkdir(parents=True,exist_ok=True)
            p.write_text(f'Assert.Contains("{OLD}", modelName);')

    def test_check_never_writes(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            ready=prepare(root)
            self.assertEqual(len(ready),9)
            self.assertFalse(any((root/rel).exists() for rel in NEW))
            self.assertEqual((root/MODIFIED[0]).read_text(),(HERE/'expected'/MODIFIED[0]).read_text())

    def test_install_updates_tests_and_no_project_backup(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            backup=install(root,prepare(root))
            self.assertTrue(backup.is_dir())
            self.assertEqual(backup.parent,root.parent)
            self.assertEqual((root/MODIFIED[0]).read_text(),(HERE/'payload'/MODIFIED[0]).read_text())
            self.assertTrue(all((root/x).is_file() for x in NEW))
            self.assertTrue(all(NEW_KEY in (root/x).read_text() for x in VERSION_TESTS))
            self.assertTrue(all((backup/x).is_file() for x in MODIFIED+VERSION_TESTS))

    def test_refuses_repeat_install(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            install(root,prepare(root))
            with self.assertRaises(ValueError): prepare(root)

    def test_refuses_local_changes(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            (root/MODIFIED[0]).write_text('user edits')
            with self.assertRaises(ValueError): prepare(root)

    def test_refuses_wrong_test_version(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            (root/VERSION_TESTS[0]).write_text('unrelated')
            with self.assertRaises(ValueError): prepare(root)

    def test_check_new_payload_guard(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo'; root.mkdir(); self.make_fixture(root)
            p=root/NEW[0]; p.parent.mkdir(parents=True,exist_ok=True); p.write_text('my code')
            with self.assertRaises(ValueError): prepare(root)

if __name__=='__main__': unittest.main(verbosity=2)
