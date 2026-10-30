#!/usr/bin/env node
/** v0.8.4 - notification deep links, additive, safe rerun and file backups. */
'use strict'
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname,'..')
const marker = 'V0804_NOTIFICATION_LINKS'
const rel = {
  api: 'backend/StudentProjects.Api/Endpoints/V08Endpoints.cs',
  model: 'frontend/src/features/v08/v08.api.ts',
  notifications: 'frontend/src/views/NotificationsV08View.vue',
  progress: 'frontend/src/views/ProjectProgressV07View.vue',
  guidance: 'frontend/src/views/GuidanceV08View.vue'
}
const helper = 'backend/StudentProjects.Api/Services/V08NotificationLinks.cs'
const fail = m => { throw new Error('v0.8.4: '+m) }
const file = p => path.join(root,p)
for (const p of [...Object.values(rel),helper]) if (!fs.existsSync(file(p))) fail('Thiếu file '+p+'. Giải nén ZIP vào thư mục dự án trước.')
if (!fs.readFileSync(file(helper),'utf8').includes('class V08NotificationLinks')) fail('Helper không đúng phiên bản.')
const original = Object.fromEntries(Object.entries(rel).map(([k,p])=>[k,fs.readFileSync(file(p),'utf8')]))
if (Object.values(original).every(s => s.includes(marker))) {
  console.log('v0.8.4 đã cài trước đó; không sửa trùng.'); process.exit(0)
}
if (Object.values(original).some(s => s.includes(marker))) fail('Một phần bản vá đã tồn tại, không ghi đè. Kiểm tra backup.')
const next = Object.fromEntries(Object.entries(original).map(([k,s])=>[k,s.replace(/\r\n/g,'\n')]))
function once(k,oldValue,newValue){
  const count=next[k].split(oldValue).length-1
  if(count !== 1) fail(rel[k]+': anchor không khớp duy nhất ('+count+'). Chưa thay đổi file nào.')
  next[k] = next[k].replace(oldValue,newValue)
}
// API: preserve all existing notification fields; add optional server-resolved URL.
once('api',
  '        return Results.Ok(new { Items = rows, UnreadCount = unread });',
  `        // ${marker}: resolve historic notifications to their original objects, with permission checks.
        var targets = await V08NotificationLinks.ResolveAsync(db, user, actor, rows.Select(n => n.Type), ct);
        var linked = rows.Select(n => new { n.Id, n.Type, n.Message, n.IsRead, n.CreatedAt,
            TargetUrl = targets.TryGetValue(n.Type, out var destination) ? destination : (string?)null });
        return Results.Ok(new { Items = linked, UnreadCount = unread });`)
// Frontend contract.
once('model',
  '  isRead: boolean\n  createdAt: string\n}',
  `  isRead: boolean
  createdAt: string
  targetUrl?: string | null // ${marker}
}`)
// Notification page: open only first-party relative routes, mark read then navigate.
once('notifications',
  "import { onMounted, ref } from 'vue'",
  `import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router' // ${marker}`)
once('notifications',
  'const items = ref<NotificationV08[]>([])',
  `const router = useRouter()
const openingId = ref('')
const items = ref<NotificationV08[]>([])`)
once('notifications',
  'async function readAll() {',
  `function canOpen(item: NotificationV08): boolean {
  // Do not navigate to external URLs, even if a response is unexpectedly malformed.
  return typeof item.targetUrl === 'string' && /^\\/(milestones|progress|submissions|evaluations|guidance)\\?/.test(item.targetUrl)
}
async function openNotification(item: NotificationV08) {
  if (!canOpen(item) || openingId.value) return
  openingId.value = item.id
  error.value = ''
  try {
    if (!item.isRead) {
      await v08Api.markRead(item.id)
      item.isRead = true
      unread.value = Math.max(0, unread.value - 1)
    }
    await router.push(item.targetUrl!)
  } catch (e) { error.value = e instanceof Error ? e.message : 'Không thể mở thông báo.' }
  finally { openingId.value = '' }
}
async function readAll() {`)
once('notifications',
  '        <div><p>{{ item.message }}</p><small>{{ fmt(item.createdAt) }}</small></div>',
  `        <div class="v08-notice-body">
          <button v-if="canOpen(item)" type="button" class="v08-notice-link" :disabled="!!openingId"
            :aria-label="'Mở nội dung thông báo: ' + item.message" @click="openNotification(item)">
            <strong>{{ item.message }}</strong><small>{{ fmt(item.createdAt) }}</small>
            <span class="v08-open-hint">Mở nội dung liên quan →</span>
          </button>
          <template v-else><p>{{ item.message }}</p><small>{{ fmt(item.createdAt) }}</small></template>
        </div>`)
once('notifications',
  '.v08-error {color:#b91c1c}button{cursor:pointer}',
  `.v08-error {color:#b91c1c}button{cursor:pointer}
/* ${marker} */
.v08-notice-body{flex:1;min-width:0}
.v08-notice-link{display:grid;gap:4px;width:100%;text-align:left;padding:2px 0;border:0;background:transparent;color:inherit;white-space:normal;cursor:pointer}
.v08-notice-link:hover strong{text-decoration:underline}
.v08-notice-link:focus-visible{outline:2px solid #2563eb;outline-offset:3px}
.v08-notice-link strong{font-weight:500}
.v08-notice-link small{font-size:.83rem}
.v08-open-hint{color:#2563eb;font-size:.82rem}`)
// Project-progress view: support explicit milestoneId (existing projectId was already supported).
once('progress',
  "const route = useRoute()",
  `const route = useRoute() // ${marker}`)
once('progress',
  "    if (!milestones.value.some(x => x.id === selectedId.value)) selectedId.value = milestones.value[0]?.id || ''",
  `    const requestedMilestone = typeof route.query.milestoneId === 'string' ? route.query.milestoneId : ''
    if (requestedMilestone && milestones.value.some(x => x.id === requestedMilestone)) selectedId.value = requestedMilestone
    else if (!milestones.value.some(x => x.id === selectedId.value)) selectedId.value = milestones.value[0]?.id || ''`)
once('progress',
  'watch(() => route.query.projectId, (id) => {',
  `watch(() => route.query.milestoneId, (id) => {
  if (typeof id === 'string' && milestones.value.some(m => m.id === id)) selectedId.value = id
})
watch(() => route.query.projectId, (id) => {`)
// Guidance: select project from deep link and focus exact session.
once('guidance',
  "import { computed, onMounted, ref, watch } from 'vue'",
  `import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router' // ${marker}`)
once('guidance',
  'const auth = useAuthStore()',
  `const auth = useAuthStore()
const route = useRoute()`)
once('guidance',
  "    else if (!projects.value.some(p => p.id === projectId.value)) projectId.value = projects.value[0]?.id ?? ''",
  `    else {
      const incoming = typeof route.query.projectId === 'string' ? route.query.projectId : ''
      if (incoming && projects.value.some(p => p.id === incoming)) projectId.value = incoming
      else if (!projects.value.some(p => p.id === projectId.value)) projectId.value = projects.value[0]?.id ?? ''
    }`)
once('guidance',
  "  try { entries.value = await v08Api.guidance(projectId.value); error.value = '' }",
  `  try {
    entries.value = await v08Api.guidance(projectId.value); error.value = ''
    await focusNotifiedEntry()
  }`)
once('guidance',
  'async function save() {',
  `async function focusNotifiedEntry() {
  const id = typeof route.query.entryId === 'string' ? route.query.entryId : ''
  if (!id || !entries.value.some(entry => entry.id === id)) return
  await nextTick()
  document.getElementById('guidance-entry-' + id)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
async function save() {`)
once('guidance',
  'watch(projectId,load)',
  `watch(projectId,load)
watch(() => route.query.projectId, id => {
  if (typeof id === 'string' && projects.value.some(p => p.id === id)) projectId.value = id
})
watch(() => route.query.entryId, () => { void focusNotifiedEntry() })`)
once('guidance',
  '    <article v-for="entry in entries" :key="entry.id">',
  `    <article v-for="entry in entries" :key="entry.id" :id="'guidance-entry-' + entry.id"
      :class="{ 'v08-notification-focus': route.query.entryId === entry.id }">`)
once('guidance',
  '.entry-head{display:flex;',
  `.v08-notification-focus{background:#eff6ff;border-left:3px solid #2563eb;padding-left:12px!important;scroll-margin-top:90px}
.entry-head{display:flex;`)
// Verify before modifying any source. Keep line-ending styles.
const modifications=Object.entries(rel).map(([k,p])=>({p,from:file(p),content: original[k].includes('\r\n') ? next[k].replace(/\n/g,'\r\n'):next[k]}))
const stamp = new Date().toISOString().replace(/[:.]/g,'-')
const backups=[]
try {
  for(const change of modifications) {
    const backup=change.from+'.before-v0804-'+stamp+'.bak'
    fs.copyFileSync(change.from,backup);backups.push({file:change.from,backup})
  }
  for(const change of modifications) {
    fs.writeFileSync(change.from,change.content)
    console.log('Cập nhật:',change.p)
  }
} catch(e) {
  for(const {file,backup} of backups) try {fs.copyFileSync(backup,file)} catch {}
  throw e
}
console.log('v0.8.4: Nhấn thông báo -> mở đúng Project/Milestone/Nhật ký. Không đổi Database.')
