/** v0.7.10: Choose insertion position and reorder editable template Milestones.
 * Requires v0.7.9. Safe, idempotent, single-component patch, backups included.
 */
'use strict'
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const file = path.join(root, 'frontend/src/components/MilestoneTemplatePlanner.vue')
const marker = '// V0710_POSITIONED_MILESTONES'
function check(condition, msg) { if (!condition) throw new Error(`v0.7.10: ${msg}`) }
check(fs.existsSync(file), 'Không tìm thấy component MilestoneTemplatePlanner.vue, cần cài v0.7.9 trước.')
const original = fs.readFileSync(file, 'utf8')
if (original.includes(marker)) {
  check(original.includes('function moveMilestone(') && original.includes('v-model.number="insertPosition"') && original.includes('entries.value.splice(target, 0, item)'), 'Phát hiện marker nhưng bản vá không đầy đủ; không tự ghi đè.')
  console.log('v0.7.10 đã được cài đặt, không thay đổi thêm.')
  process.exit(0)
}
check(original.includes('// V079_EXTRA_CUSTOM_MILESTONES'), 'Bạn cần cài v0.7.9 trước.')
let code = original.replace(/\r\n/g, '\n')
function once(from,to,context) {
  const index = code.indexOf(from)
  check(index >= 0, `Không tìm thấy vị trí ${context}.`)
  check(code.indexOf(from, index + from.length) === -1, `Có nhiều vị trí ${context}, dừng để bảo vệ mã hiện tại.`)
  code = code.slice(0,index)+to+code.slice(index+from.length)
}
const funcs = `${marker}
// -1: add to end. Otherwise, index of the gap where the new milestone will be placed.
const insertPosition = ref(-1)
function moveMilestone(key: string, delta: -1 | 1) {
  if (busy.value) return
  const from = entries.value.findIndex(item => item.key === key)
  const target = from + delta
  if (from < 0 || target < 0 || target >= entries.value.length) return
  const [item] = entries.value.splice(from, 1)
  entries.value.splice(target, 0, item)
  // Reset the insertion control to end after manual reordering.
  insertPosition.value = -1
  error.value = ''
}
`
once('let customMilestoneCounter = 0',funcs+'let customMilestoneCounter = 0','state và chức năng sắp xếp')
once(`  entries.value.push({
    key, checked: true, title: '', description: '', expectedResult: '', deadlineDay: ''
  })
  error.value = ''`, `  const position = insertPosition.value < 0
    ? entries.value.length
    : Math.max(0, Math.min(insertPosition.value, entries.value.length))
  entries.value.splice(position, 0, {
    key, checked: true, title: '', description: '', expectedResult: '', deadlineDay: ''
  })
  // Add the next custom Milestone immediately after the one just inserted.
  if (insertPosition.value >= 0) insertPosition.value = position + 1
  error.value = ''`, 'chèn mốc tùy chỉnh')
const oldButton='      <button type="button" :disabled="busy || entries.length >= 48" @click="addCustomMilestone">+ Thêm Milestone tùy chỉnh</button>'
const newButton=`      <label class="insert-position-label" for="template-insert-position">Vị trí chèn Milestone mới
        <select id="template-insert-position" v-model.number="insertPosition" :disabled="busy || entries.length >= 48">
          <option :value="-1">Cuối danh sách</option>
          <option :value="0">Đầu danh sách (trước mốc 1)</option>
          <option v-for="(item, i) in entries" :key="'insert-after-' + item.key" :value="i + 1">Sau mốc {{ i + 1 }}{{ item.title.trim() ? ' · ' + item.title.trim() : '' }}</option>
        </select>
      </label>
      <button type="button" :disabled="busy || entries.length >= 48" @click="addCustomMilestone">+ Thêm Milestone tùy chỉnh</button>`
once(oldButton,newButton,'ô chọn vị trí thêm mới')
const oldHeader='            <button type="button" class="button-quiet" :disabled="busy" @click="removeItem(item.key)">Bỏ khỏi bản kế hoạch</button>'
const newHeader=`            <div class="item-head-actions">
              <button type="button" class="button-quiet" :disabled="busy || i === 0" :aria-label="'Đưa mốc ' + (i + 1) + ' lên trên'" @click="moveMilestone(item.key, -1)">↑ Lên</button>
              <button type="button" class="button-quiet" :disabled="busy || i === entries.length - 1" :aria-label="'Đưa mốc ' + (i + 1) + ' xuống dưới'" @click="moveMilestone(item.key, 1)">↓ Xuống</button>
              <button type="button" class="button-quiet" :disabled="busy" @click="removeItem(item.key)">Bỏ khỏi bản kế hoạch</button>
            </div>`
once(oldHeader,newHeader,'nút điều chỉnh thứ tự')
once('.template-card{', '.item-head-actions{display:flex;flex-wrap:wrap;gap:8px;align-items:center}.insert-position-label{flex:1;min-width:225px}.template-card{', 'style của UI')
// Write only after all anchors have been validated and modified.
const backup = `${file}.before-v0710-${new Date().toISOString().replace(/[:.]/g,'-')}.bak`
fs.copyFileSync(file, backup)
try {
  fs.writeFileSync(file, original.includes('\r\n') ? code.replace(/\n/g, '\r\n') : code, 'utf8')
  console.log('Đã cập nhật vị trí thêm mốc tùy chỉnh và chức năng Lên/Xuống:',path.relative(root,file))
  console.log('Sao lưu:', path.relative(root,backup))
  console.log('Không thay đổi API, database hay dữ liệu Milestone đã lưu.')
} catch (err) { fs.copyFileSync(backup,file); throw err }
