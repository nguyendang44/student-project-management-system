import importlib.util
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('main_navigation', Path(__file__).parents[1] / 'apply_main_navigation.py')
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

MODULES = '''export type ModuleId =
  | 'dashboard'
  | 'ai'
  | 'notifications'
export const modules = [
  {
    "id": "ai",
    "path": "/ai-analysis"
  },
  {
    "id": "notifications",
    "path": "/notifications"
  }
]
'''
ROUTER = '''const moduleRoutes = modules.map((mod) => ({
  component: mod.id === 'ai' ? () => import('../views/CodeAnalysisV11View.vue')
  : mod.id === 'github' ? () => import('../views/GitHubV10View.vue')
  : () => import('../views/ModuleView.vue'),
}))
const children = [{ path: 'profile', component: () => import('../views/OwnProfileView.vue') },
       { path: 'git-progress', component: () => import('../views/GitProgressV11View.vue'),
         meta: { title: 'Git Progress Analysis', allowedRoles: ['Student', 'Lecturer', 'Admin'] } },
       ...moduleRoutes]
'''
DASHBOARD = '''<p><RouterLink to="/notifications">Xem thông báo</RouterLink> · <RouterLink to="/guidance">Nhật ký hướng dẫn</RouterLink></p>'''
LAYOUT = '''const visible = modules.filter(m => auth.role && m.roles.includes(auth.role))
<RouterLink v-for="item in group.items" :key="item.id" :to="item.path">{{ item.title }}</RouterLink>'''


def make_repo(root):
    (root / '.git').mkdir()
    for path, content in [(mod.MODULES, MODULES), (mod.ROUTER, ROUTER), (mod.DASHBOARD, DASHBOARD), (mod.LAYOUT, LAYOUT), (mod.VIEW, '<template><section>Git Progress</section></template>')]:
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding='utf-8')


class InstallerTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'student-project-management-system'
        self.root.mkdir()
        make_repo(self.root)

    def tearDown(self):
        self.temp.cleanup()

    def test_main_sidebar_route_and_dashboard(self):
        result = mod.prepare(self.root)
        m, r, d = [result[p][1] for p in mod.FILES]
        self.assertIn("| 'gitprogress'", m)
        self.assertIn('"group": "Tích hợp"', m)
        self.assertIn('"path": "/git-progress"', m)
        self.assertIn('"roles": ["Student", "Lecturer", "Admin"]', m)
        self.assertIn("mod.id === 'gitprogress'", r)
        self.assertEqual(r.count('GitProgressV11View.vue'), 1)
        self.assertNotIn("path: 'git-progress'", r)
        self.assertIn('to="/git-progress"', d)

    def test_prepare_is_read_only(self):
        before = {p: (self.root / p).read_bytes() for p in mod.FILES}
        mod.prepare(self.root)
        self.assertEqual(before, {p: (self.root / p).read_bytes() for p in mod.FILES})

    def test_apply_backup_and_refuse_duplicate(self):
        before = {p: (self.root / p).read_bytes() for p in mod.FILES}
        backup = mod.apply(self.root, mod.prepare(self.root))
        self.assertTrue(backup.is_dir())
        for path in mod.FILES:
            self.assertEqual((backup / path).read_bytes(), before[path])
        with self.assertRaisesRegex(ValueError, 'already registered'):
            mod.prepare(self.root)

    def test_missing_view_refuses(self):
        (self.root / mod.VIEW).unlink()
        with self.assertRaisesRegex(ValueError, 'Missing required file'):
            mod.prepare(self.root)

    def test_changed_route_refuses_without_writing(self):
        path = self.root / mod.ROUTER
        path.write_text(path.read_text().replace("title: 'Git Progress Analysis'", "title: 'Custom'"))
        before = {p: (self.root / p).read_bytes() for p in mod.FILES}
        with self.assertRaisesRegex(ValueError, 'Manual /git-progress route'):
            mod.prepare(self.root)
        self.assertEqual(before, {p: (self.root / p).read_bytes() for p in mod.FILES})

    def test_existing_main_layout_required(self):
        (self.root / mod.LAYOUT).write_text('custom component')
        with self.assertRaisesRegex(ValueError, 'sidebar no longer'):
            mod.prepare(self.root)


if __name__ == '__main__':
    unittest.main()
