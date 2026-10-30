// SPMS v0.10 additive patch installer (Node 18+). Do not delete scripts or reset DB.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const bundle = JSON.parse(fs.readFileSync(path.join(__dirname, 'github-v010.patch.json'), 'utf8'));
const updates = new Map();
const original = new Map();
function read(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) throw new Error('File source không tồn tại: ' + rel);
  const data = fs.readFileSync(file, 'utf8');
  original.set(rel, data);
  return data;
}
function prepare(rel, data) { updates.set(rel, data); }
function replaceOnce(text, from, to, target) {
  const first = text.indexOf(from);
  if (first < 0 || text.indexOf(from, first + from.length) >= 0)
    throw new Error('Không tìm được đúng một vị trí cần sửa trong ' + target + '. Dừng để tránh ghi đè source khác phiên bản.');
  return text.slice(0, first) + to + text.slice(first + from.length);
}
function alter(rel, marker, patchFn) {
  const source = read(rel);
  prepare(rel, source.includes(marker) ? source : patchFn(source));
}
try {
  alter('backend/StudentProjects.Api/Program.cs', 'V010_GITHUB_PUBLIC_INTEGRATION', s => {
    s = replaceOnce(s, 'builder.Services.AddSingleton<V08NotificationEngine>();',
      '// V010_GITHUB_PUBLIC_INTEGRATION\n' +
      'builder.Services.AddMemoryCache();\n' +
      'builder.Services.AddHttpClient<IGitHubPublicClient, GitHubPublicClient>(http =>\n' +
      '{\n    http.BaseAddress = new Uri("https://api.github.com/");\n    http.Timeout = TimeSpan.FromSeconds(10);\n' +
      '}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });\n' +
      'builder.Services.AddSingleton<V08NotificationEngine>();', 'Program.cs');
    return replaceOnce(s, 'app.MapV08Endpoints();',
      'app.MapV08Endpoints();\napp.MapGitHubV10Endpoints();', 'Program.cs');
  });
  alter('backend/StudentProjects.Api/Endpoints/SkeletonEndpoints.cs', 'V010_GITHUB_STUBS_REMOVED', s => {
    let count = 0;
    const updated = s.split(/(?<=\n)/).filter(line => {
      const remove = /api\.Map(Get|Post)\("\/repositories(?:\/\{id:guid\}\/commits)?"/.test(line);
      if (remove) count++;
      return !remove;
    }).join('');
    if (count !== 3) throw new Error('SkeletonEndpoints.cs: mong đợi đúng 3 stub GitHub nhưng có ' + count);
    return updated.replace('public static void MapSkeletonEndpoints(this IEndpointRouteBuilder app)',
      '// V010_GITHUB_STUBS_REMOVED\n    public static void MapSkeletonEndpoints(this IEndpointRouteBuilder app)');
  });
  alter('frontend/src/app/router.ts', "mod.id === 'github' ? () => import('../views/GitHubV10View.vue')", s =>
    replaceOnce(s, "component: mod.id === 'dashboard' ? () => import('../views/DashboardV08View.vue')",
      "component: mod.id === 'github' ? () => import('../views/GitHubV10View.vue')\n" +
      "    : mod.id === 'dashboard' ? () => import('../views/DashboardV08View.vue')", 'router.ts'));
  alter('frontend/src/app/modules.ts', 'GET /v10/projects/{projectId}/repositories', s =>
    replaceOnce(s, '"GET /repositories",\n      "POST /repositories"',
      '"GET /v10/projects/{projectId}/repositories",\n' +
      '      "POST /v10/projects/{projectId}/repositories",\n' +
      '      "GET /v10/repositories/{id}/commits",\n' +
      '      "GET /v10/repositories/{id}/branches",\n' +
      '      "DELETE /v10/repositories/{id}"', 'modules.ts'));
  for (const [rel, content] of Object.entries(bundle)) {
    const dest = path.join(root, rel);
    if (fs.existsSync(dest)) original.set(rel, fs.readFileSync(dest, 'utf8'));
    prepare(rel, content);
  }
  const modified = [...updates].filter(([rel, content]) => !original.has(rel) || original.get(rel) !== content);
  if (!modified.length) {
    console.log('v0.10 already installed; no source changes.');
    process.exit(0);
  }
  const stamp = new Date().toISOString().replace(/[.:]/g, '-');
  const backupRoot = path.join(root, 'scripts', 'v010-backups', stamp);
  // Backup ALL pre-existing targets before touching any source file.
  for (const [rel] of modified) {
    if (!original.has(rel)) continue;
    const target = path.join(backupRoot, rel);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, original.get(rel));
  }
  const written = [];
  try {
    for (const [rel, content] of modified) {
      const target = path.join(root, rel);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, content, 'utf8');
      written.push(rel);
    }
  } catch (e) {
    for (const rel of written.reverse()) {
      if (original.has(rel)) fs.writeFileSync(path.join(root, rel), original.get(rel), 'utf8');
      else fs.rmSync(path.join(root, rel), { force: true });
    }
    throw e;
  }
  console.log('v0.10 installed: ' + modified.length + ' changed source files.');
  console.log('Source backups: ' + backupRoot);
  console.log('NEXT: run scripts/upgrade-github-v010.sql in SSMS against StudentProjectsDev.');
  console.log('Then restart backend and frontend; run tests/build. Database was NOT modified by installer.');
} catch (e) {
  console.error('INSTALL FAILED (preflight or write): ' + e.message);
  process.exitCode = 1;
}
