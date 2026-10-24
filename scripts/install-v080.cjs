#!/usr/bin/env node
/** v0.8 installer for exact v0.7.12 baseline. New files are shipped in ZIP.
 * Anchored, additive, idempotent and backed-up. SQL is separately applied by upgrade-v080.ps1.
 */
'use strict'
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const marker = '// V080_AUTOMATION_GUIDANCE'
const names = {
  program: 'backend/StudentProjects.Api/Program.cs',
  context: 'backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs',
  modules: 'frontend/src/app/modules.ts',
  router: 'frontend/src/app/router.ts',
  layout: 'frontend/src/components/AppLayout.vue'
}
const required = [
  'backend/StudentProjects.Api/Endpoints/V08Endpoints.cs',
  'backend/StudentProjects.Api/Services/V08NotificationEngine.cs',
  'backend/StudentProjects.Domain/Entities/GuidanceSession.cs',
  'frontend/src/features/v08/v08.api.ts',
  'frontend/src/views/DashboardV08View.vue',
  'frontend/src/views/NotificationsV08View.vue',
  'frontend/src/views/GuidanceV08View.vue',
  'frontend/src/features/progress/sortMilestones.ts',
  'scripts/upgrade-v080.sql',
  'scripts/upgrade-v080.ps1'
]
function fail(s){ throw new Error('v0.8: '+s) }
for(const name of required) if(!fs.existsSync(path.join(root,name))) fail('Thiếu file mới '+name+' (hãy giải nén ZIP vào thư mục dự án).')
const originals = Object.fromEntries(Object.entries(names).map(([key,name]) => {
  const file = path.join(root,name)
  if(!fs.existsSync(file)) fail('Thiếu '+name)
  return [key,fs.readFileSync(file,'utf8')]
}))
const already = Object.values(originals).map(s=>s.includes(marker))
if(already.every(Boolean)) {console.log('v0.8 đã cài, không sửa trùng.'); process.exit(0)}
if(already.some(Boolean)) fail('Phát hiện v0.8 chỉ cài một phần. Không ghi đè. Kiểm tra backup trước.')
if (!fs.readFileSync(path.join(root,'frontend/src/features/progress/sortMilestones.ts'),'utf8').includes('V0712_SORT_MILESTONES'))
  fail('Yêu cầu v0.7.12 (thiếu marker sắp xếp Milestone).')
let next = Object.fromEntries(Object.entries(originals).map(([k,v])=>[k,v.replace(/\r\n/g,'\n')]))
function inject(k,anchor,replacement){
  const n=next[k].split(anchor).length-1
  if(n!==1) fail(`${names[k]}: cần đúng một điểm chèn (${n}): ${anchor.slice(0,60)}`)
  next[k]=next[k].replace(anchor,replacement)
}
inject('program','using StudentProjects.Api.Endpoints;',
  'using StudentProjects.Api.Endpoints;\nusing StudentProjects.Api.Services;\n'+marker)
inject('program','builder.Services.AddAuthorization();',
  'builder.Services.AddAuthorization();\nbuilder.Services.AddSingleton<V08NotificationEngine>();\nbuilder.Services.AddHostedService<V08NotifierWorker>();')
inject('program','app.MapProjectProgressV07Endpoints();',
  'app.MapProjectProgressV07Endpoints();\napp.MapV08Endpoints();')
inject('context','    public DbSet<Notification> Notifications => Set<Notification>();',
  '    public DbSet<Notification> Notifications => Set<Notification>();\n    public DbSet<GuidanceSession> GuidanceSessions => Set<GuidanceSession>(); // '+marker)
inject('context','        modelBuilder.Entity<AutomationRun>(e =>',
`        modelBuilder.Entity<GuidanceSession>(e =>
        {
            e.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(4000).IsRequired();
            e.Property(x => x.NextActions).HasMaxLength(4000).IsRequired();
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.ProjectId, x.MeetingAt });
        });
        modelBuilder.Entity<AutomationRun>(e =>`)
inject('context','        modelBuilder.Entity<Notification>().ToTable(t => t.ExcludeFromMigrations());',
  '        modelBuilder.Entity<Notification>().ToTable(t => t.ExcludeFromMigrations());\n        modelBuilder.Entity<GuidanceSession>().ToTable(t => t.ExcludeFromMigrations());')
inject('modules',"  | 'notifications'", "  | 'notifications'\n  | 'guidance' // "+marker)
inject('modules','export const modules: ModuleSpec[] = [',
`export const modules: ModuleSpec[] = [
  { id: 'guidance', path: '/guidance', group: 'Dự án',
    title: 'Nhật ký hướng dẫn', description: 'Lịch sử các buổi gặp và xác nhận của giảng viên',
    roles: ['Student','Lecturer','Admin'], requirements: 'v0.8', useCases: [], columns: [], endpoints: ['GET /v08/projects/{id}/guidance'] },`)
// Apply the new 3 routes ahead of the existing router dispatch, leaving all legacy routes intact.
const componentAnchor='  component: '
if (!next.router.includes('ProjectProgressV07View.vue')) fail('Router chưa có v0.7')
inject('router',componentAnchor,
`  ${marker}
  component: mod.id === 'dashboard' ? () => import('../views/DashboardV08View.vue')
    : mod.id === 'notifications' ? () => import('../views/NotificationsV08View.vue')
    : mod.id === 'guidance' ? () => import('../views/GuidanceV08View.vue')
    : `)
// In the layout, render a compact bell indicator without altering other menu or profile controls.
inject('layout',"import { computed } from 'vue'", "import { computed, ref, onMounted, onUnmounted } from 'vue'\nimport { v08Api } from '../features/v08/v08.api'\n"+marker)
inject('layout','const route = useRoute()',
`const unreadV08 = ref(0)
let v08Timer: ReturnType<typeof setInterval> | undefined
async function reloadUnreadV08() {
  try { unreadV08.value = (await v08Api.notifications()).unreadCount }
  catch { /* notifications may not be upgraded yet; don't break existing site */ }
}
onMounted(() => { void reloadUnreadV08(); v08Timer = setInterval(() => { void reloadUnreadV08() }, 60000) })
onUnmounted(() => { if (v08Timer) clearInterval(v08Timer) })
const route = useRoute()`)
inject('layout','<div class="account-summary">',
  '<div class="account-summary"><RouterLink to="/notifications" title="Thông báo mới">Thông báo <strong v-if="unreadV08">({{ unreadV08 }})</strong></RouterLink>')
// Do NOT replace global UI scaffolding, local style or v0.7 milestone components.
const changes=Object.entries(names).map(([key,name])=>({name,file:path.join(root,name),content:originals[key].includes('\r\n')?next[key].replace(/\n/g,'\r\n'):next[key]}))
const backups=[]; const stamp=new Date().toISOString().replace(/[:.]/g,'-')
try {
  for(const x of changes) {
    const backup=x.file+'.before-v080-'+stamp+'.bak'
    fs.copyFileSync(x.file,backup);backups.push([x.file,backup]);
  }
  for(const x of changes) {fs.writeFileSync(x.file,x.content);console.log('Cập nhật:',x.name)}
  console.log('v0.8 installed. Run PowerShell SQL schema upgrade before launching Backend.')
} catch(e) {
  for(const [file,backup] of backups) try{ fs.copyFileSync(backup,file) }catch{}
  throw e
}
