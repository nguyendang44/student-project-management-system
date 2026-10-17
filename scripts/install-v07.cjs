#!/usr/bin/env node
// Additive installer: preserve existing v0.6.14 routes, menus, APIs and customizations.
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const server = path.join(root, 'backend/StudentProjects.Api/Program.cs')
const router = path.join(root, 'frontend/src/app/router.ts')
const context = path.join(root, 'backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs')
const entity = path.join(root, 'backend/StudentProjects.Domain/Entities/CoreEntities.cs')
const endpoint = path.join(root, 'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs')
const client = path.join(root, 'frontend/src/features/progress/projectProgress.api.ts')
const view = path.join(root, 'frontend/src/views/ProjectProgressV07View.vue')
const modules = path.join(root, 'frontend/src/app/modules.ts')
function fail(s) { throw new Error('Không cài v0.7: ' + s) }
for(const p of [server,router,context,entity,endpoint,client,view,modules]) if(!fs.existsSync(p)) fail('Thiếu file: ' + path.relative(root,p))
const serverOriginal=fs.readFileSync(server,'utf8')
const routerOriginal=fs.readFileSync(router,'utf8')
const contextSource=fs.readFileSync(context,'utf8')
const entitySource=fs.readFileSync(entity,'utf8')
const modSource=fs.readFileSync(modules,'utf8')
for(const name of ['Milestones','ProgressUpdates','MilestoneSubmissions','ProjectEvaluations','Projects','AuditEntries'])
 if(!contextSource.includes(name)) fail('DbContext hiện tại thiếu '+name+'; không chèn code chưa tương thích.')
for(const name of ['class Milestone','class ProgressUpdate','class MilestoneSubmission','class ProjectEvaluation'])
 if(!entitySource.includes(name)) fail('CoreEntities.cs không có '+name+'.')
for(const id of ['milestones','progress','submissions','evaluations'])
 if(!modSource.includes(`'${id}'`) && !modSource.includes(`"${id}"`)) fail('Menu hiện tại thiếu module '+id+'.')
const registered = 'app.MapProjectProgressV07Endpoints();'
const routeMarker = "'../views/ProjectProgressV07View.vue'"
const haveServer = serverOriginal.includes(registered)
const haveRoute = routerOriginal.includes(routeMarker)
if (haveServer !== haveRoute) fail('Phát hiện v0.7 mới được cài một phần. Kiểm tra Program.cs / router.ts trước.')
if (haveServer && haveRoute) { console.log('v0.7 đã được cài. Không ghi đè.'); process.exit(0) }
const usingLine = 'using StudentProjects.Api.Endpoints;'
if(!serverOriginal.includes(usingLine)) fail('Program.cs thiếu using StudentProjects.Api.Endpoints; cần kiểm tra thủ công.')
const registrationAnchor = 'app.MapGlobalLecturerCapacityEndpoints();'
if(!serverOriginal.includes(registrationAnchor)) fail('Chưa thấy API v0.6.14; hãy kiểm tra nâng cấp v0.6.14 trước.')
if(serverOriginal.split(registrationAnchor).length!==2) fail('Program.cs có nhiều vị trí đăng ký v0.6.14.')
const serverUpdated=serverOriginal.replace(registrationAnchor,registrationAnchor+'\n'+registered)
const pattern = /component:\s*mod\.id\s*===\s*['"]users['"]\s*\?/m
if (!pattern.test(routerOriginal)) fail('Không tìm thấy nhánh Router modules của v0.6.14; không sửa nhầm router.')
const routerUpdated=routerOriginal.replace(pattern, match =>
  `component: ['milestones', 'progress', 'submissions', 'evaluations'].includes(mod.id)\n    ? () => import('../views/ProjectProgressV07View.vue')\n    : mod.id === 'users' ?`)
if(!serverUpdated.includes(registered) || !routerUpdated.includes(routeMarker)) fail('Không tạo được cấu hình v0.7.')
const now=new Date().toISOString().replace(/[:.]/g,'-')
const changes=[{file:server, content:serverUpdated},{file:router, content:routerUpdated}]
// Back up BOTH before touching either; rollback on write error.
const backups=[]
try {
 for(const c of changes){ const backup=c.file+'.before-v07-'+now+'.bak'; fs.copyFileSync(c.file,backup);backups.push({file:c.file,backup}); }
 for(const c of changes) fs.writeFileSync(c.file,c.content,'utf8')
 console.log('Đã cài v0.7:')
 console.log('  '+path.relative(root,server))
 console.log('  '+path.relative(root,router))
 console.log('Backup: '+backups.map(x=>path.relative(root,x.backup)).join(', '))
 console.log('Tiếp theo: chạy scripts/upgrade-project-progress-v07.ps1, dotnet build, dotnet test và npm run build.')
} catch(e) {
 for(const b of backups) try { fs.copyFileSync(b.backup,b.file) } catch { /* report original error */ }
 throw e
}
