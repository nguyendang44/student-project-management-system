<script setup lang="ts">
import { computed, ref } from 'vue'
import { HttpError } from '../api/client'
import { projectProgressApi, type MilestoneForm } from '../features/progress/projectProgress.api'
import { milestoneTemplateCategories } from '../features/progress/milestoneTemplates'

type EditableTemplate = {
  key: string
  checked: boolean
  title: string
  description: string
  expectedResult: string
  deadlineDay: string
}
const props = defineProps<{
  projectId: string
  role: 'Student' | 'Lecturer'
  existingTitles: string[]
}>()
const emit = defineEmits<{
  applied: [count: number]
  close: []
}>()
const categoryId = ref(milestoneTemplateCategories[0].id)
const entries = ref<EditableTemplate[]>([])
const busy = ref(false)
const error = ref('')
const selectedEntries = computed(() => entries.value.filter(item => item.checked))
const activeCategory = computed(() => milestoneTemplateCategories.find(c => c.id === categoryId.value))

// V0711_REPLACE_CATEGORY_DRAFT
function addCategory() {
  const category = activeCategory.value
  if (!category || busy.value) return
  // Switching a category does not change the draft until the user applies it.
  // Warn before replacing edited or custom milestones in the current draft.
  if (entries.value.length && !window.confirm(
    `Thay toàn bộ ${entries.value.length} Milestone trong bản nháp bằng 6 Milestone mẫu của nhóm “${category.name}”? Những nội dung chưa lưu sẽ bị mất.`
  )) return
  entries.value = category.milestones.map((template, i) => ({
    key: `${category.id}-${i}`,
    checked: true,
    title: template.title,
    description: template.description,
    expectedResult: template.expectedResult,
    deadlineDay: ''
  }))
  insertPosition.value = -1
  bulkDay.value = ''
  error.value = ''
}
// V079_EXTRA_CUSTOM_MILESTONES
// V0710_POSITIONED_MILESTONES
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
let customMilestoneCounter = 0
function addCustomMilestone() {
  if (busy.value) return
  if (entries.value.length >= 48) {
    error.value = 'Một bản kế hoạch có tối đa 48 mốc. Hãy áp dụng bớt mốc trước khi tạo thêm.'
    return
  }
  let key: string
  do { key = `custom-${++customMilestoneCounter}` } while (entries.value.some(x => x.key === key))
  const position = insertPosition.value < 0
    ? entries.value.length
    : Math.max(0, Math.min(insertPosition.value, entries.value.length))
  entries.value.splice(position, 0, {
    key, checked: true, title: '', description: '', expectedResult: '', deadlineDay: ''
  })
  // Add the next custom Milestone immediately after the one just inserted.
  if (insertPosition.value >= 0) insertPosition.value = position + 1
  error.value = ''
}
function removeItem(key: string) {
  if (busy.value) return
  entries.value = entries.value.filter(x => x.key !== key)
}
function applyOneDate(day: string) {
  if (!day || busy.value) return
  entries.value.filter(x => x.checked).forEach(x => { x.deadlineDay = day })
}
const bulkDay = ref('')
function normalName(value: string) { return value.trim().replace(/\s+/g, ' ').toLocaleLowerCase('vi-VN') }
function validDay(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const [y, m, d] = value.split('-').map(Number)
  const parsed = new Date(Date.UTC(y, m - 1, d))
  return parsed.getUTCFullYear() === y && parsed.getUTCMonth() + 1 === m && parsed.getUTCDate() === d
}
function payload() : MilestoneForm[] | null {
  const selected = selectedEntries.value
  if (!selected.length) { error.value = 'Hãy chọn ít nhất một Milestone để áp dụng.'; return null }
  if (selected.length > 48) { error.value = 'Tối đa 48 Milestone trong một lần áp dụng.'; return null }
  const names = new Set(props.existingTitles.map(normalName))
  for (let i = 0; i < selected.length; i++) {
    const x = selected[i]
    const title = x.title.trim()
    if (title.length < 1 || title.length > 300 || x.description.trim().length > 4000 || x.expectedResult.trim().length > 2000) {
      error.value = `Mốc ${i + 1}: kiểm tra tên (1–300 ký tự), mô tả (≤4000) và kết quả cần đạt (≤2000).`
      return null
    }
    const k = normalName(title)
    if (names.has(k)) { error.value = `Milestone “${title}” đã tồn tại hoặc bị trùng trong bộ mẫu. Hãy đổi tên hoặc bỏ chọn.`; return null }
    names.add(k)
    if (!validDay(x.deadlineDay)) { error.value = `Vui lòng chọn ngày Deadline hợp lệ cho “${title}”.`; return null }
  }
  return selected.map(x => ({
    title: x.title.trim(), description: x.description.trim(), expectedResult: x.expectedResult.trim(),
    // Explicit Vietnam civil date. The backend also normalizes deadline to the final tick of this day.
    startAt: new Date(`${x.deadlineDay}T00:00:00+07:00`).toISOString(),
    deadlineAt: new Date(`${x.deadlineDay}T23:59:59.999+07:00`).toISOString()
  }))
}
async function apply() {
  if (busy.value) return
  error.value = ''
  const items = payload()
  if (!items) return
  if (!window.confirm(`Tạo ${items.length} Milestone ${props.role === 'Student' ? 'đề xuất để giảng viên duyệt' : 'cho Project hiện tại'}?`)) return
  busy.value = true
  try {
    const result = await projectProgressApi.createBatch(props.projectId, items)
    emit('applied', result.createdCount)
  } catch (e) {
    error.value = e instanceof HttpError ? (e.payload?.message || `API lỗi (${e.status})`)
      : e instanceof Error ? e.message : 'Không thể áp dụng bộ mẫu.'
  } finally { busy.value = false }
}
</script>

<template>
  <section class="planner" aria-label="Thư viện mẫu Milestone">
    <div class="planner-head">
      <div>
        <h4>Thư viện mẫu Milestone theo loại đề tài</h4>
        <p class="muted">Chọn nhóm, xem trước và sửa từng mốc trước khi áp dụng. Không sửa đề tài gốc.</p>
      </div>
      <button type="button" class="button-quiet" :disabled="busy" @click="emit('close')">Đóng</button>
    </div>
    <div class="category-row">
      <label for="template-category">Phân loại đề tài
        <select id="template-category" v-model="categoryId" :disabled="busy">
          <option v-for="category in milestoneTemplateCategories" :key="category.id" :value="category.id">{{ category.name }}</option>
        </select>
      </label>
      <button type="button" :disabled="busy" @click="addCategory">Dùng 6 Milestone của nhóm (thay bản nháp)</button>
      <label class="insert-position-label" for="template-insert-position">Vị trí chèn Milestone mới
        <select id="template-insert-position" v-model.number="insertPosition" :disabled="busy || entries.length >= 48">
          <option :value="-1">Cuối danh sách</option>
          <option :value="0">Đầu danh sách (trước mốc 1)</option>
          <option v-for="(item, i) in entries" :key="'insert-after-' + item.key" :value="i + 1">Sau mốc {{ i + 1 }}{{ item.title.trim() ? ' · ' + item.title.trim() : '' }}</option>
        </select>
      </label>
      <button type="button" :disabled="busy || entries.length >= 48" @click="addCustomMilestone">+ Thêm Milestone tùy chỉnh</button>
    </div>
    <p class="muted" v-if="activeCategory">Ví dụ: {{ activeCategory.examples }}. Chọn nhóm mới rồi bấm “Dùng 6 Milestone” sẽ thay các mốc hiện tại trong bản nháp. Milestone đã lưu vào Project không bị ảnh hưởng.</p>
    <template v-if="entries.length">
      <div class="bulk-deadline">
        <label for="template-bulk-day">Đặt ngày Deadline chung (tùy chọn)
          <input id="template-bulk-day" v-model="bulkDay" :disabled="busy" type="date" />
        </label>
        <button type="button" :disabled="busy || !bulkDay" @click="applyOneDate(bulkDay)">Áp dụng ngày cho các mốc đã chọn</button>
        <span class="muted">Hạn của mỗi mốc tính đến hết ngày (23:59:59 giờ Việt Nam).</span>
      </div>
      <form @submit.prevent="apply">
        <div v-for="(item,i) in entries" :key="item.key" class="template-card">
          <div class="item-head">
            <label class="checkbox-label"><input v-model="item.checked" type="checkbox" :disabled="busy" /> Chọn mốc {{ i + 1 }}</label>
            <div class="item-head-actions">
              <button type="button" class="button-quiet" :disabled="busy || i === 0" :aria-label="'Đưa mốc ' + (i + 1) + ' lên trên'" @click="moveMilestone(item.key, -1)">↑ Lên</button>
              <button type="button" class="button-quiet" :disabled="busy || i === entries.length - 1" :aria-label="'Đưa mốc ' + (i + 1) + ' xuống dưới'" @click="moveMilestone(item.key, 1)">↓ Xuống</button>
              <button type="button" class="button-quiet" :disabled="busy" @click="removeItem(item.key)">Bỏ khỏi bản kế hoạch</button>
            </div>
          </div>
          <div class="form-grid" :class="{mutedItem: !item.checked}">
            <label>Tên Milestone <input v-model="item.title" :disabled="busy || !item.checked" maxlength="300" required /></label>
            <label>Deadline (chỉ ngày) <input v-model="item.deadlineDay" :disabled="busy || !item.checked" type="date" required /></label>
            <label class="full">Mô tả <textarea v-model="item.description" :disabled="busy || !item.checked" rows="2" maxlength="4000" /></label>
            <label class="full">Kết quả cần đạt <textarea v-model="item.expectedResult" :disabled="busy || !item.checked" rows="2" maxlength="2000" /></label>
          </div>
        </div>
        <p v-if="error" class="error" role="alert">{{ error }}</p>
        <div class="footer">
          <span>{{ selectedEntries.length }} / {{ entries.length }} mốc được chọn</span>
          <div class="footer-buttons">
            <button type="button" class="button-quiet" :disabled="busy" @click="emit('close')">Hủy</button>
            <button type="submit" :disabled="busy || !selectedEntries.length">{{ busy ? 'Đang lưu…' : (role === 'Student' ? 'Gửi bộ đề xuất cho giảng viên' : 'Áp dụng Milestone cho Project') }}</button>
          </div>
        </div>
      </form>
    </template>
    <p v-else class="muted">Chọn “Dùng 6 Milestone của nhóm” để bắt đầu bằng mẫu, hoặc “+ Thêm Milestone tùy chỉnh” để tạo mốc riêng.</p>
  </section>
</template>

<style scoped>
.planner{margin:18px 0 0;padding:20px;border:1px solid var(--border-color,#d5dbe4);border-radius:12px;background:var(--card-bg,#fff);color:inherit}
.planner-head{display:flex;justify-content:space-between;align-items:start;gap:14px}.planner-head h4{margin:0 0 6px;font-size:1.05rem}.muted{color:var(--text-muted,#637083);font-size:.9rem;margin:8px 0 12px}.category-row,.bulk-deadline{display:flex;flex-wrap:wrap;align-items:end;gap:12px;margin:12px 0}.category-row label{flex:1;min-width:220px}.bulk-deadline{padding:12px;background:var(--surface-secondary,#f6f8fa);border-radius:8px}.bulk-deadline label{min-width:180px}
.planner label{display:flex;flex-direction:column;gap:6px;font-weight:500;font-size:.93rem}.planner input:not([type=checkbox]),.planner textarea,.planner select{width:100%;min-width:0;padding:9px 11px;border:1px solid #bcc5d2;border-radius:6px;background:var(--field-bg,#fff);color:inherit;font:inherit;box-sizing:border-box}.planner button{padding:9px 13px;border:1px solid #9ba7b6;border-radius:7px;cursor:pointer;font:inherit;background:var(--button-bg,#f4f6f8);color:inherit}.planner button[type=submit]{background:#174f8b;color:white;border-color:#174f8b}.planner button:disabled{opacity:.5;cursor:not-allowed}.planner .button-quiet{background:transparent}
.item-head-actions{display:flex;flex-wrap:wrap;gap:8px;align-items:center}.insert-position-label{flex:1;min-width:225px}.template-card{margin:14px 0;border:1px solid var(--border-color,#d5dbe4);border-radius:9px;padding:15px}.item-head{display:flex;gap:12px;justify-content:space-between;align-items:center;margin-bottom:12px}.checkbox-label{flex-direction:row!important;align-items:center;font-weight:650!important}.checkbox-label input{width:17px;height:17px}.form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.form-grid .full{grid-column:1/-1}.mutedItem{opacity:.65}.error{color:#ad2020;background:#ffefef;padding:10px;border-radius:6px}.footer{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:14px;margin-top:18px}.footer-buttons{display:flex;gap:10px}
@media(max-width:690px){.planner{padding:12px}.form-grid{grid-template-columns:1fr}.form-grid .full{grid-column:1}.item-head{flex-wrap:wrap}.footer-buttons{width:100%}}
</style>
