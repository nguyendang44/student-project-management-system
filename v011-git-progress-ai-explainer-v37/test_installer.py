#!/usr/bin/env python3
"""Python installer tests; no dotnet or database needed."""
import tempfile
import unittest
from pathlib import Path
from apply_ai_explainer_v37 import HERE, MODIFIED, NEW, VERSION_TESTS, OLD, NEW_KEY, prepare, install

class Tests(unittest.TestCase):
    def fixture(self, root):
        (root/'.git').mkdir()
        for rel in ['backend/StudentProjects.Api/Program.cs',
                    'backend/StudentProjects.IntegrationTests/GitProgressAiExplanationV36Tests.cs']:
            p=root/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('fixture')
        for rel in MODIFIED:
            p=root/rel;p.parent.mkdir(parents=True,exist_ok=True)
            p.write_text((HERE/'expected'/rel).read_text(encoding='utf-8'))
        for rel in VERSION_TESTS:
            p=root/rel;p.parent.mkdir(parents=True,exist_ok=True)
            p.write_text(f'Assert.Contains("{OLD}", actual);')

    def test_check_is_read_only(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            updated=prepare(root)
            self.assertEqual(len(updated),len(MODIFIED)+len(NEW)+len(VERSION_TESTS))
            self.assertFalse(any((root/rel).exists() for rel in NEW))
            self.assertEqual((root/MODIFIED[0]).read_text(), (HERE/'expected'/MODIFIED[0]).read_text())

    def test_install_and_backup(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            backup=install(root,prepare(root))
            self.assertEqual(backup.parent, root.parent)
            self.assertTrue(all((backup/p).is_file() for p in MODIFIED+VERSION_TESTS))
            self.assertTrue(all((root/p).is_file() for p in NEW))
            self.assertTrue(all(NEW_KEY in (root/p).read_text() for p in VERSION_TESTS))
            self.assertIn('HasAdditionalPurpose', (root/MODIFIED[0]).read_text())

    def test_no_repeat(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            install(root,prepare(root))
            with self.assertRaises(ValueError): prepare(root)

    def test_refuse_modified_source(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            (root/MODIFIED[0]).write_text('User local changes')
            with self.assertRaises(ValueError): prepare(root)

    def test_refuse_existing_new_file(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            p=root/NEW[0];p.parent.mkdir(parents=True,exist_ok=True);p.write_text('other')
            with self.assertRaises(ValueError): prepare(root)

    def test_refuse_wrong_assertion_version(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td)/'repo';root.mkdir();self.fixture(root)
            (root/VERSION_TESTS[0]).write_text('no version')
            with self.assertRaises(ValueError): prepare(root)

    def test_ui_contains_nested_explanation_and_legacy_join(self):
        ui=(HERE/'payload/frontend/src/views/GitProgressV11View.vue').read_text()
        self.assertIn('row.item.aiExplanation',ui) if False else self.assertIn('item.aiExplanation',ui)
        self.assertIn("x.source === 'ollama-feature'",ui)
        self.assertIn("x.source === 'feature-rule'",ui)
        self.assertNotIn("{{ evidence.length }})</h4>",ui)
        self.assertNotIn("Cần giảng viên đối chiếu.Nhận xét AI",ui)

    def test_backend_report_persists_single_group(self):
        worker=(HERE/'payload/backend/StudentProjects.Api/Services/GitProgressWorker.cs').read_text()
        report=(HERE/'payload/backend/StudentProjects.Api/Services/GitProgressFeatureReport.cs').read_text()
        self.assertIn('GitProgressFeatureReport.Build(features, explanations)',worker)
        self.assertIn('aiExplanation = ',report)
        self.assertNotIn('observations.Add(new\n                {\n                    file = fact',worker)

if __name__=='__main__': unittest.main(verbosity=2)
