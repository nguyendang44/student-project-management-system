/** v0.7.9: Add custom Milestones (7, 8, 9, ...) to v0.7.8 template planner.
 * Run from project root: node scripts/install-extra-milestones-v079.cjs
 * Only modifies the template planner; idempotent and creates backup.
 */
'use strict'
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const file = path.join(root, 'frontend/src/components/MilestoneTemplatePlanner.vue')
const tag = '// V079_EXTRA_CUSTOM_MILESTONES'
function assert(ok, message) { if (!ok) throw new Error(`v0.7.9: ${message}`) }
assert(fs.existsSync(file), 'Không tìm thấy MilestoneTemplatePlanner.vue. Cài v0.7.8 trước.')
const raw = fs.readFileSync(file, 'utf8')
if (raw.includes(tag)) {
  assert(raw.includes('@click="addCustomMilestone"') && raw.includes('function addCustomMilestone()'), 'Phát hiện marker nhưng mã chưa đầy đủ; cần kiểm tra file thủ công.')
  console.log('v0.7.9 đã được cài trước đó, không sửa trùng.')
  process.exit(0)
}
const normalized = raw.replace(/\r\n/g, '\n')
function replaceOnce(source, from, to, label) {
  const first = source.indexOf(from)
  assert(first >= 0, `Không tìm thấy vị trí chèn ${label}. Không ghi đè mã cũ.`)
  assert(source.indexOf(from, first + from.length) === -1, `Nhiều vị trí ${label}; không thể vá an toàn.`)
  return source.slice(0, first) + to + source.slice(first + from.length)
}
const fn = `// V079_EXTRA_CUSTOM_MILESTONES\nlet customMilestoneCounter = 0\nfunction addCustomMilestone() {\n  if (busy.value) return\n  if (entries.value.length >= 48) {\n    error.value = 'Một bản kế hoạch có tối đa 48 mốc. Hãy áp dụng bớt mốc trước khi tạo thêm.'\n    return\n  }\n  let key: string\n  do { key = \`custom-\${++customMilestoneCounter}\` } while (entries.value.some(x => x.key === key))\n  entries.value.push({\n    key, checked: true, title: '', description: '', expectedResult: '', deadlineDay: ''\n  })\n  error.value = ''\n}\n`
let out = replaceOnce(normalized, 'function removeItem(key: string) {', fn+'function removeItem(key: string) {', 'hàm thêm mốc')
const button = '      <button type="button" :disabled="busy || entries.length >= 48" @click="addCustomMilestone">+ Thêm Milestone tùy chỉnh</button>'
out = replaceOnce(out,
  '      <button type="button" :disabled="busy" @click="addCategory">Thêm 6 Milestone của nhóm</button>',
  '      <button type="button" :disabled="busy" @click="addCategory">Thêm 6 Milestone của nhóm</button>\n'+button,
  'nút thêm mốc tùy chỉnh')
out = replaceOnce(out,
  '<p v-else class="muted">Bấm “Thêm 6 Milestone của nhóm” để xem và chỉnh sửa bộ mẫu.</p>',
  '<p v-else class="muted">Chọn “Thêm 6 Milestone của nhóm” để sử dụng mẫu, hoặc “+ Thêm Milestone tùy chỉnh” để tạo mốc riêng.</p>',
  'lời hướng dẫn khi chưa có mốc')
const backup = `${file}.before-v079-${new Date().toISOString().replace(/[:.]/g,'-')}.bak`
fs.copyFileSync(file, backup)
try {
  fs.writeFileSync(file, raw.includes('\r\n') ? out.replace(/\n/g, '\r\n') : out, 'utf8')
  console.log('Đã thêm nút Milestone tùy chỉnh cho Student/Lecturer.')
  console.log('Đã sao lưu:', path.relative(root, backup))
  console.log('Giữ nguyên Backend/API, dữ liệu và bộ 48 mẫu của v0.7.8.')
} catch (e) { fs.copyFileSync(backup, file); throw e }
