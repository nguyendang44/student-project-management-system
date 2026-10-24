/** v0.7.11: Replace the six draft milestones when the selected category is applied.
 * Requires v0.7.10; edits only MilestoneTemplatePlanner.vue, preserves custom insertion/reordering.
 */
'use strict'
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const file = path.join(root, 'frontend', 'src', 'components', 'MilestoneTemplatePlanner.vue')
const marker = '// V0711_REPLACE_CATEGORY_DRAFT'
const fail = msg => { throw new Error(`v0.7.11: ${msg}`) }
if (!fs.existsSync(file)) fail('Không tìm thấy frontend/src/components/MilestoneTemplatePlanner.vue. Hãy cài v0.7.10 trước.')
const source = fs.readFileSync(file, 'utf8')
if (source.includes(marker)) {
  if (!source.includes('entries.value = category.milestones.map(')) fail('Bản vá v0.7.11 chưa đầy đủ; hãy kiểm tra thủ công.')
  console.log('v0.7.11 đã cài, không thay đổi thêm.')
  process.exit(0)
}
if (!source.includes('// V0710_POSITIONED_MILESTONES')) fail('Cần cài v0.7.10 trước.')
let code = source.replace(/\r\n/g, '\n')
function replaceOnce(oldText, newText, reason) {
  const first = code.indexOf(oldText)
  if (first === -1) fail(`Không tìm thấy đoạn ${reason}; đã dừng để tránh sửa sai.`)
  if (code.indexOf(oldText, first + oldText.length) !== -1) fail(`Có nhiều đoạn ${reason}; đã dừng để tránh sửa sai.`)
  code = code.slice(0, first) + newText + code.slice(first + oldText.length)
}
const oldFunc = `function addCategory() {
  const category = activeCategory.value
  if (!category || busy.value) return
  let inserted = 0
  category.milestones.forEach((template, i) => {
    const key = \`\${category.id}-\${i}\`
    if (entries.value.some(x => x.key === key)) return
    entries.value.push({ key, checked: true, title: template.title,
      description: template.description, expectedResult: template.expectedResult, deadlineDay: '' })
    inserted += 1
  })
  error.value = inserted ? '' : 'Sáu mốc của nhóm này đã có trong bản kế hoạch. Bạn có thể tiếp tục chỉnh sửa phía dưới.'
}`
const newFunc = `${marker}
function addCategory() {
  const category = activeCategory.value
  if (!category || busy.value) return
  // Switching a category does not change the draft until the user applies it.
  // Warn before replacing edited or custom milestones in the current draft.
  if (entries.value.length && !window.confirm(
    \`Thay toàn bộ \${entries.value.length} Milestone trong bản nháp bằng 6 Milestone mẫu của nhóm “\${category.name}”? Những nội dung chưa lưu sẽ bị mất.\`
  )) return
  entries.value = category.milestones.map((template, i) => ({
    key: \`\${category.id}-\${i}\`,
    checked: true,
    title: template.title,
    description: template.description,
    expectedResult: template.expectedResult,
    deadlineDay: ''
  }))
  insertPosition.value = -1
  bulkDay.value = ''
  error.value = ''
}`
replaceOnce(oldFunc,newFunc,'hàm addCategory phiên bản cũ')
replaceOnce('Thêm 6 Milestone của nhóm</button>', 'Dùng 6 Milestone của nhóm (thay bản nháp)</button>', 'nhãn nút thêm nhóm')
replaceOnce('Có thể kết hợp nhiều nhóm; mốc đã chỉnh sửa sẽ không bị ghi đè khi thêm nhóm khác.', 'Chọn nhóm mới rồi bấm “Dùng 6 Milestone” sẽ thay các mốc hiện tại trong bản nháp. Milestone đã lưu vào Project không bị ảnh hưởng.', 'hướng dẫn cách thêm nhóm')
replaceOnce('Chọn “Thêm 6 Milestone của nhóm” để sử dụng mẫu, hoặc “+ Thêm Milestone tùy chỉnh” để tạo mốc riêng.', 'Chọn “Dùng 6 Milestone của nhóm” để bắt đầu bằng mẫu, hoặc “+ Thêm Milestone tùy chỉnh” để tạo mốc riêng.', 'hướng dẫn khi chưa có mốc')
const backup = `${file}.before-v0711-${new Date().toISOString().replace(/[:.]/g,'-')}.bak`
fs.copyFileSync(file, backup)
try {
  fs.writeFileSync(file, source.includes('\r\n') ? code.replace(/\n/g,'\r\n') : code, 'utf8')
  console.log('Đã cập nhật:', path.relative(root,file))
  console.log('Sao lưu:', path.relative(root,backup))
  console.log('Chỉ thay bản nháp trên giao diện; không thay đổi API, database hay Milestone đã lưu.')
} catch (e) { fs.copyFileSync(backup, file); throw e }
