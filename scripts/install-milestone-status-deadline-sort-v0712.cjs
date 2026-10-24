/** v0.7.12: Sort Milestones consistently on Project list, Progress picker, Project overview.
 * Apply on v0.7.11; frontend only; safely anchored, backed-up and idempotent.
 */
'use strict'
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const viewPath = path.join(root, 'frontend/src/views/ProjectProgressV07View.vue')
const overviewPath = path.join(root, 'frontend/src/components/ProjectMilestoneSummary.vue')
const helperPath = path.join(root, 'frontend/src/features/progress/sortMilestones.ts')
const marker = '// V0712_SORT_MILESTONES'
const fail = message => { throw new Error('v0.7.12: ' + message) }
for (const file of [viewPath,overviewPath]) if (!fs.existsSync(file)) fail('Không có ' + path.relative(root,file))
const originals = new Map([[viewPath,fs.readFileSync(viewPath,'utf8')],[overviewPath,fs.readFileSync(overviewPath,'utf8')]])
const helper = `${marker}
import type { MilestoneV07 } from './projectProgress.api'

/** Display-only ordering: confirmed plans first, then plans requiring review/revision.
 * Within each group, earliest deadline first. Never mutates the API response.
 */
export function sortMilestonesForDisplay(items: readonly MilestoneV07[]): MilestoneV07[] {
  const group = (m: MilestoneV07) => m.proposalStatus === 'CONFIRMED' ? 0 : 1
  const due = (m: MilestoneV07) => {
    const t = Date.parse(m.deadlineAt)
    return Number.isFinite(t) ? t : Number.MAX_SAFE_INTEGER
  }
  return [...items].sort((a, b) => group(a) - group(b) || due(a) - due(b))
}
`
if (fs.existsSync(helperPath) && !fs.readFileSync(helperPath,'utf8').includes(marker)) fail('sortMilestones.ts đã tồn tại nhưng không thuộc bản vá này. Chưa thay đổi gì.')
const change = (file, oldAnchor, replacement, details) => {
  const raw = originals.get(file)
  let text = raw.replace(/\r\n/g, '\n')
  if (text.includes(marker)) return {skipped:true, text:raw}
  if (!text.includes("import { projectProgressApi, type ProjectV07, type MilestoneV07")) fail(`${details}: không nhận diện được import API; chưa sửa file.`)
  const occurrences = text.split(oldAnchor).length - 1
  if (occurrences !== 1) fail(`${details}: cần đúng một điểm chèn, tìm thấy ${occurrences}; chưa sửa file.`)
  text = text.replace("import { projectProgressApi, type ProjectV07, type MilestoneV07", `import { sortMilestonesForDisplay } from '../features/progress/sortMilestones'\n${marker}\nimport { projectProgressApi, type ProjectV07, type MilestoneV07`)
  text = text.replace(oldAnchor, replacement)
  return {skipped:false, text: raw.includes('\r\n') ? text.replace(/\n/g,'\r\n') : text}
}
const updates = new Map()
updates.set(viewPath, change(viewPath,
  'milestones.value = await projectProgressApi.milestones(projectId.value)',
  'milestones.value = sortMilestonesForDisplay(await projectProgressApi.milestones(projectId.value))',
  'Trang quản lý và cập nhật tiến độ'))
updates.set(overviewPath, change(overviewPath,
  'if (projectId.value === id) milestones.value = data',
  'if (projectId.value === id) milestones.value = sortMilestonesForDisplay(data)',
  'Tiến độ tổng hợp dự án'))
for (const [file, result] of updates) {
  if (result.skipped && !result.text.includes('sortMilestonesForDisplay(')) fail(path.relative(root,file)+' có marker nhưng thiếu logic sort.')
}
if (fs.existsSync(helperPath) && fs.readFileSync(helperPath,'utf8') !== helper) fail('Helper đã được tùy chỉnh; không ghi đè. Hãy kiểm tra thủ công.')
if (!fs.existsSync(path.dirname(helperPath))) fail('Không có frontend/src/features/progress. Hãy kiểm tra đúng thư mục dự án.')
const changed = [...updates.entries()].filter(([,r])=>!r.skipped)
if (!changed.length) { console.log('v0.7.12 đã được cài. Không sửa trùng.');process.exit(0) }
const backups = new Map()
const stamp = new Date().toISOString().replace(/[:.]/g,'-')
let createdHelper = false
try {
  if (!fs.existsSync(helperPath)) { fs.writeFileSync(helperPath,helper,'utf8');createdHelper = true }
  for (const [file,result] of changed) {
    const backup = `${file}.before-v0712-${stamp}.bak`
    fs.copyFileSync(file, backup)
    backups.set(file,backup)
    fs.writeFileSync(file,result.text,'utf8')
    console.log('Đã cập nhật:',path.relative(root,file))
    console.log('Sao lưu:',path.relative(root,backup))
  }
} catch (e) {
  for (const [file,backup] of backups) fs.copyFileSync(backup,file)
  if (createdHelper) fs.rmSync(helperPath,{force:true})
  throw e
}
console.log('Thứ tự: kế hoạch đã xác nhận theo deadline tăng dần; kế hoạch chờ duyệt/sửa ở cuối cũng theo deadline.')
console.log('Không sửa database, API, trạng thái Milestone hay dữ liệu đã lưu.')
