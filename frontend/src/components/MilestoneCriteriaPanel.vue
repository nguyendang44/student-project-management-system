<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { milestoneCriteriaApi, type MilestoneCriterion, type CriterionPlanDraft, type CriterionWrite } from '../features/progress/milestoneCriteria.api'

const props = defineProps<{ milestoneId: string; canPlan: boolean; canCheck: boolean; canReview: boolean }>()
const emit = defineEmits<{ changed: [] }>()
const details = ref<Awaited<ReturnType<typeof milestoneCriteriaApi.details>> | null>(null)
const percentText = (value: number) => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)
const points = computed(() => details.value?.criteria.reduce((sum, criterion) => sum + criterion.points, 0) ?? 0)
const form = ref({ title: '', acceptanceCriteria: '', points: 10 })
const newDeadline = ref('')
const editingDeadlineId = ref('')
const deadlineDraft = ref('')
const formOpen = ref(false)
const reviewExpanded = ref<Record<string, boolean>>({})
const notes = ref<Record<string, string>>({})
const reviewFeedback = ref<Record<string, { type: 'error' | 'success'; message: string }>>({})
const pending = ref('')
const planDraft = ref<(CriterionPlanDraft & { key: number; checked: boolean; day: string })[]>([])
const editingId = ref('')
const editForm = ref({ title: '', acceptanceCriteria: '', points: 10, day: '' })
const error = ref('')
const info = ref('')
const lifetime = new AbortController()
let refreshTimer: ReturnType<typeof setInterval> | undefined
let reloadSequence = 0
let lastRefresh = 0
let polling = false
const checkState = computed(() => details.value?.check?.state ?? 'idle')
const checking = computed(() => checkState.value === 'queued' || checkState.value === 'running')
const checkLabel = computed(() => ({ idle: 'Chưa kiểm tra', queued: 'Chờ kiểm tra', running: 'Đang kiểm tra lại',
  completed: 'Hoàn thành kiểm tra', failed: 'Kiểm tra chưa thành công', cancelled: 'Đã dừng kiểm tra' }[checkState.value]))
const checkTime = computed(() => {
  const value = details.value?.check?.completedAt ?? details.value?.check?.startedAt ?? details.value?.check?.requestedAt
  return value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : ''
})

watch(checkState, (state, previous) => {
  if (!previous || !info.value.startsWith('Đã gửi yêu cầu kiểm tra')) return
  if (state === 'completed') info.value = 'Đã hoàn thành kiểm tra và cập nhật kết quả.'
  else if (state === 'failed' || state === 'cancelled') info.value = ''
})

function errorText(e: unknown): string { return e instanceof Error ? e.message : 'Có lỗi khi cập nhật đầu việc.' }
async function reload() {
  const sequence = ++reloadSequence
  const value = await milestoneCriteriaApi.details(props.milestoneId, lifetime.signal)
  if (!lifetime.signal.aborted && sequence === reloadSequence) {
    details.value = value
    lastRefresh = Date.now()
  }
}
async function perform(kind: string, action: () => Promise<unknown>, message: string) {
  pending.value = kind
  error.value = ''; info.value = ''
  try {
    await action()
    if (lifetime.signal.aborted) return
    emit('changed')
    await reload()
    info.value = message
  } catch (e) {
    if (!lifetime.signal.aborted) error.value = errorText(e)
  } finally {
    pending.value = ''
  }
}
function add() {
  const deadlineAt = newDeadline.value ? parseDeadline(newDeadline.value) : null
  if (newDeadline.value && !deadlineAt) { error.value = 'Chọn ngày hoàn thành đầu việc hợp lệ.'; return }
  void perform('add', async () => {
    await milestoneCriteriaApi.add(props.milestoneId, { ...form.value, deadlineAt })
    form.value = { title: '', acceptanceCriteria: '', points: 10 }
    newDeadline.value = ''
    formOpen.value = false
  }, 'Đã thêm đầu việc.')
}
async function generatePlan() {
  if (pending.value) return
  if (planDraft.value.length && !window.confirm('Thay bản nháp hiện tại bằng đề xuất AI mới?')) return
  pending.value = 'plan-ai'; error.value = ''; info.value = ''
  try {
    const result = await milestoneCriteriaApi.suggest(props.milestoneId, lifetime.signal)
    if (lifetime.signal.aborted) return
    planDraft.value = result.items.map((item, key) => ({ ...item, key, checked: true, day: '' }))
    info.value = 'Đề xuất đang ở bản nháp. Kiểm tra, sửa nội dung và chọn các đầu việc cần lưu.'
  } catch (e) { if (!lifetime.signal.aborted) error.value = errorText(e) }
  finally { pending.value = '' }
}
function savePlan() {
  const selected = planDraft.value.filter(item => item.checked)
  if (!selected.length) { error.value = 'Chọn ít nhất một đầu việc để lưu.'; return }
  const items: CriterionWrite[] = []
  for (const item of selected) {
    const deadlineAt = item.day ? parseDeadline(item.day) : null
    if (item.day && !deadlineAt) { error.value = 'Kiểm tra ngày hoàn thành trong bản nháp.'; return }
    items.push({ title: item.title, acceptanceCriteria: item.acceptanceCriteria, points: item.points, deadlineAt })
  }
  void perform('plan-save', async () => {
    await milestoneCriteriaApi.savePlan(props.milestoneId, items)
    planDraft.value = []
  }, 'Đã lưu các đầu việc được chọn.')
}
function edit(criterion: MilestoneCriterion) {
  editingId.value = criterion.id
  editForm.value = { title: criterion.title, acceptanceCriteria: criterion.acceptanceCriteria,
    points: criterion.points, day: criterion.deadlineAt
      ? new Date(new Date(criterion.deadlineAt).getTime() + 7 * 60 * 60 * 1000).toISOString().slice(0, 10) : '' }
}
function saveEdit(criterion: MilestoneCriterion) {
  const deadlineAt = editForm.value.day ? parseDeadline(editForm.value.day) : null
  if (editForm.value.day && !deadlineAt) { error.value = 'Chọn ngày hoàn thành hợp lệ.'; return }
  void perform('edit', async () => {
    await milestoneCriteriaApi.edit(props.milestoneId, criterion.id, { title: editForm.value.title,
      acceptanceCriteria: editForm.value.acceptanceCriteria, points: editForm.value.points, deadlineAt })
    editingId.value = ''
  }, 'Đã lưu đầu việc và tính lại tiến độ. Nội dung hoặc điều kiện đạt đã sửa cần được đánh giá lại.')
}
function remove(criterion: MilestoneCriterion) {
  if (!window.confirm(`Xóa đầu việc “${criterion.title}”? Tiến độ sẽ được tính lại theo tổng trọng số còn lại.`)) return
  void perform('remove', () => milestoneCriteriaApi.remove(props.milestoneId, criterion.id), 'Đã xóa đầu việc và tính lại tiến độ.')
}
const dateText = (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
function parseDeadline(day: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return null
  const date = new Date(`${day}T23:59:59.999+07:00`)
  if (!Number.isFinite(date.getTime())) return null
  const localDay = new Date(date.getTime() + 7 * 60 * 60 * 1000).toISOString().slice(0, 10)
  return localDay === day ? date.toISOString() : null
}
function editDeadline(criterion: MilestoneCriterion) {
  editingDeadlineId.value = criterion.id
  deadlineDraft.value = criterion.deadlineAt
    ? new Date(new Date(criterion.deadlineAt).getTime() + 7 * 60 * 60 * 1000).toISOString().slice(0, 10) : ''
}
function saveDeadline(criterion: MilestoneCriterion) {
  const deadlineAt = parseDeadline(deadlineDraft.value)
  if (!deadlineAt) { error.value = 'Chọn ngày hoàn thành đầu việc hợp lệ.'; return }
  void perform('deadline', async () => {
    await milestoneCriteriaApi.deadline(props.milestoneId, criterion.id, deadlineAt)
    editingDeadlineId.value = ''
  }, 'Đã lưu deadline của đầu việc.')
}
async function recheck() {
  if (checking.value || pending.value || !criteriaToCheck.value.length) return
  pending.value = 'recheck'; error.value = ''; info.value = ''
  try {
    const result = await milestoneCriteriaApi.recheck(props.milestoneId)
    if (lifetime.signal.aborted) return
    await reload()
    emit('changed')
    info.value = result.skipped ? result.message ?? 'GV đã xác nhận toàn bộ đầu việc, không cần gửi AI.'
      : `Đã gửi yêu cầu kiểm tra ${result.pendingCriteria ?? criteriaToCheck.value.length} đầu việc của Milestone đang mở. Kết quả sẽ tự cập nhật.`
  } catch (e) { if (!lifetime.signal.aborted) error.value = errorText(e) }
  finally { pending.value = '' }
}
async function review(criterion: MilestoneCriterion, isMet: boolean, proposalId: string | null) {
  if (pending.value) return
  const note = (notes.value[criterion.id] ?? '').trim()
  const proposal = latest(criterion, 'AI')
  const needsNote = !isMet || !proposal || proposal.id !== proposalId || proposal.outcome !== 'full'
  if (note.length > 1000 || (needsNote && !note)) {
    reviewFeedback.value[criterion.id] = { type: 'error', message: note.length > 1000
      ? 'Ghi chú tối đa 1000 ký tự.'
      : isMet ? 'Nhập căn cứ GV đã kiểm tra vào ô ghi chú rồi bấm Xác nhận hoàn thành.'
        : 'Nhập lý do cần bổ sung vào ô ghi chú.' }
    document.getElementById(`review-note-${criterion.id}`)?.focus()
    return
  }
  pending.value = criterion.id
  error.value = ''; info.value = ''
  delete reviewFeedback.value[criterion.id]
  let saved = false
  try {
    await milestoneCriteriaApi.review(props.milestoneId, criterion.id, isMet, proposalId, note)
    saved = true
    if (lifetime.signal.aborted) return
    delete notes.value[criterion.id]
    await reload()
    if (isMet) reviewExpanded.value[criterion.id] = false
    emit('changed')
    const updated = details.value?.criteria.find(item => item.id === criterion.id)
    reviewFeedback.value[criterion.id] = { type: 'success', message: isMet
      ? updated && typeof updated.awardedPoints === 'number'
        ? `Đã xác nhận hoàn thành. Điểm được tính: ${updated.awardedPoints}/${updated.points}.`
        : 'Đã lưu xác nhận. Hãy tải lại trang để xem điểm hiện tại.'
      : 'Đã lưu yêu cầu bổ sung và cập nhật lại điểm.' }
  } catch (e) {
    if (!lifetime.signal.aborted) reviewFeedback.value[criterion.id] = { type: 'error', message: saved
      ? 'Đã lưu xác nhận nhưng chưa tải lại được điểm. Hãy tải lại trang để xem kết quả.'
      : errorText(e) }
  } finally { pending.value = '' }
}
const latest = (criterion: MilestoneCriterion, source: 'AI' | 'REVIEW') =>
  (source === 'AI'
    ? criterion.history.find(assessment => assessment.source === source && assessment.isCurrent !== false && assessment.outcome === 'full')
      ?? criterion.history.find(assessment => assessment.source === source && assessment.isCurrent !== false)
    : criterion.history.find(assessment => assessment.source === source && assessment.isCurrent !== false))
const criteriaToCheck = computed(() => details.value?.criteria.filter(criterion => latest(criterion, 'REVIEW')?.outcome !== 'full') ?? [])
const teacherConfirmedCount = computed(() => (details.value?.criteria.length ?? 0) - criteriaToCheck.value.length)
const outcome = (source: 'AI' | 'REVIEW', value: string) => source === 'REVIEW'
  ? (value === 'full' ? 'Đã xác nhận hoàn thành' : 'Cần bổ sung')
  : ({ full: 'Có bằng chứng', partial: 'Có bằng chứng một phần', missing: 'Chưa đủ dữ liệu' }[value] ?? value)
onMounted(async () => {
  try {
    await reload()
    refreshTimer = setInterval(() => {
      if (pending.value || polling || (!checking.value && Date.now() - lastRefresh < 15000)) return
      polling = true
      const previous = JSON.stringify([details.value?.check, details.value?.criteria.map(c => [c.id, c.history[0]?.id, c.awardedPoints, c.deadlineWarning?.code])])
      void reload().then(() => {
        if (previous !== JSON.stringify([details.value?.check, details.value?.criteria.map(c => [c.id, c.history[0]?.id, c.awardedPoints, c.deadlineWarning?.code])])) emit('changed')
      }).catch(() => {}).finally(() => { polling = false })
    }, 3000)
  }
  catch (e) { if (!lifetime.signal.aborted) error.value = errorText(e) }
})
onUnmounted(() => { lifetime.abort(); if (refreshTimer) clearInterval(refreshTimer) })
</script>

<template>
  <section class="criteria-panel" aria-label="Đầu việc và bằng chứng">
    <h4>Đầu việc và bằng chứng</h4>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="info" role="status">{{ info }}</p>
    <template v-if="details">
      <div class="milestone-summary">
        <div class="summary-heading"><span>Tiến độ Milestone</span><strong>{{ percentText(details.confirmedPercent) }}%</strong></div>
        <span v-if="details.criteria.length && details.confirmedPercent >= 100" class="completion-badge milestone-state">Đã hoàn thành</span>
        <progress :value="details.confirmedPercent" max="100" aria-label="Tiến độ Milestone" />
        <div class="summary-stats">
          <span>{{ details.criteria.filter(c => c.effectiveOutcome === 'full').length }}/{{ details.criteria.length }} đầu việc đạt</span>
          <span>Tổng trọng số <strong>{{ points }}</strong></span>
          <span v-if="details.proposedPercent != null">Bằng chứng AI <strong>{{ percentText(details.proposedPercent) }}%</strong></span>
        </div>
        <p v-if="details.criteria.length && teacherConfirmedCount === details.criteria.length" class="review-success">Hoàn thành theo xác nhận giảng viên.</p>
      </div>
      <details v-if="details.criteria.length" class="calculation-help">
        <summary>Cách tính tiến độ</summary>
        <p>Tiến độ = tổng trọng số đầu việc đã đạt / {{ points }} × 100%. Mục một phần hoặc thiếu bằng chứng nhận 0 điểm.</p>
        <p>AI chỉ đối chiếu đầu việc của Milestone đang mở với các báo cáo Git hoàn tất của dự án. Đầu việc GV đã xác nhận hoàn thành được bỏ qua. Quyết định giảng viên được ưu tiên; khi giảng viên sửa, sinh viên nhận thông báo.</p>
      </details>
      <p v-if="!details.criteria.length">Chưa có đầu việc để tính tiến độ cho mốc này.</p>
      <button v-if="canPlan && details.criteria.length < 20" type="button" :disabled="!!pending" @click="generatePlan">
        {{ pending === 'plan-ai' ? 'AI đang đề xuất đầu việc…' : 'AI tạo đầu việc từ dự án và Milestone' }}
      </button>
      <form v-if="canPlan && planDraft.length" class="criteria-form" @submit.prevent="savePlan">
        <h5>Bản nháp đầu việc do AI đề xuất</h5>
        <div v-for="item in planDraft" :key="item.key" class="criterion">
          <label><input v-model="item.checked" type="checkbox" :disabled="!!pending"> Chọn đầu việc</label>
          <label>Tên đầu việc <input v-model.trim="item.title" maxlength="200" required :disabled="!item.checked || !!pending"></label>
          <label>Điều kiện đạt <textarea v-model.trim="item.acceptanceCriteria" maxlength="250" required :disabled="!item.checked || !!pending" /></label>
          <label>Trọng số <input v-model.number="item.points" type="number" min="1" max="100" required :disabled="!item.checked || !!pending"></label>
          <label>Deadline (có thể đặt sau) <input v-model="item.day" type="date" :disabled="!item.checked || !!pending"></label>
        </div>
        <div class="criteria-actions">
          <button :disabled="!!pending">Lưu các đầu việc đã chọn</button>
          <button type="button" :disabled="!!pending" @click="planDraft = []">Bỏ bản nháp</button>
        </div>
      </form>
      <button v-if="canPlan && !formOpen && details.criteria.length < 20" type="button"
        :disabled="!!pending" @click="formOpen = true">+ Thêm đầu việc</button>
      <form v-if="canPlan && formOpen" class="criteria-form" @submit.prevent="add">
        <label>Tên đầu việc <input v-model.trim="form.title" required maxlength="200"></label>
        <label>Điều kiện đạt <textarea v-model.trim="form.acceptanceCriteria" required maxlength="250" rows="2" /></label>
        <label>Trọng số trong Milestone
          <input v-model.number="form.points" type="number" min="1" max="100" required>
        </label>
        <label>Deadline của đầu việc (có thể đặt sau) <input v-model="newDeadline" type="date"></label>
        <div class="criteria-actions">
          <button :disabled="!!pending">Lưu đầu việc</button>
          <button type="button" :disabled="!!pending" @click="formOpen = false">Hủy</button>
        </div>
      </form>
      <div v-if="details.criteria.length" class="check-status" :class="'check-' + checkState"
        role="status" aria-live="polite" :aria-busy="checking || pending === 'recheck'">
        <p><span v-if="checking || pending === 'recheck'" class="check-spinner" aria-hidden="true" />
          <strong>{{ pending === 'recheck' ? 'Đang gửi yêu cầu kiểm tra…' : checkLabel }}</strong>
          <span v-if="checkTime && pending !== 'recheck'"> · {{ checkTime }}</span>
        </p>
        <p>{{ details.check?.message ?? 'Bấm kiểm tra lại để AI đối chiếu các đầu việc.' }}</p>
      </div>
      <p v-if="details.criteria.length">Phạm vi kiểm tra: <strong>{{ criteriaToCheck.length }}</strong> đầu việc của Milestone này.
        Bỏ qua <strong>{{ teacherConfirmedCount }}</strong> đầu việc GV đã xác nhận hoàn thành.</p>
      <div v-if="canCheck && details.criteria.length" class="criteria-actions">
        <button type="button" :disabled="!!pending || checking || !criteriaToCheck.length" @click="recheck">
          {{ !criteriaToCheck.length ? 'GV đã xác nhận toàn bộ đầu việc' : pending === 'recheck' ? 'Đang gửi yêu cầu…' : checkState === 'running' ? 'Đang kiểm tra lại…' : checkState === 'queued' ? 'Đang chờ kiểm tra…' : 'Kiểm tra lại tiến độ' }}
        </button>
      </div>
      <article v-for="criterion in details.criteria" :key="criterion.id" class="criterion" :class="{ 'criterion-complete': criterion.effectiveOutcome === 'full' }">
        <div class="criterion-heading"><h5>{{ criterion.title }}</h5>
          <span class="score-badge" :class="{ 'score-complete': criterion.effectiveOutcome === 'full' }">{{ criterion.awardedPoints }}/{{ criterion.points }} điểm</span>
        </div>
        <div class="criterion-meta"><span>Trọng số {{ criterion.points }} · {{ percentText(criterion.percentOfMilestone) }}% Milestone</span>
          <span>Deadline: {{ criterion.deadlineAt ? dateText(criterion.deadlineAt) : 'Chưa đặt' }}</span>
        </div>
        <p v-if="latest(criterion, 'REVIEW')?.outcome === 'full'" class="completion-badge">✓ GV đã xác nhận hoàn thành</p>
        <p>{{ criterion.acceptanceCriteria }}</p>
        <details v-if="canPlan" class="task-tools">
          <summary>Thao tác đầu việc</summary>
          <div class="criteria-actions">
          <button type="button" :disabled="!!pending" @click="edit(criterion)">Sửa đầu việc</button>
          <button type="button" :disabled="!!pending" @click="remove(criterion)">Xóa đầu việc</button>
          </div>
          <button type="button" :disabled="!!pending" @click="editDeadline(criterion)">Đặt hoặc sửa deadline</button>
        </details>
        <form v-if="canPlan && editingId === criterion.id" class="criteria-form" @submit.prevent="saveEdit(criterion)">
          <label>Tên đầu việc <input v-model.trim="editForm.title" maxlength="200" required :disabled="!!pending"></label>
          <label>Điều kiện đạt <textarea v-model.trim="editForm.acceptanceCriteria" maxlength="250" required :disabled="!!pending" /></label>
          <label>Trọng số <input v-model.number="editForm.points" type="number" min="1" max="100" required :disabled="!!pending"></label>
          <label>Deadline <input v-model="editForm.day" type="date" :disabled="!!pending"></label>
          <p>Đổi tên hoặc điều kiện đạt sẽ yêu cầu đánh giá lại nội dung mới.</p>
          <div class="criteria-actions">
            <button :disabled="!!pending">Lưu sửa đổi</button>
            <button type="button" :disabled="!!pending" @click="editingId = ''">Hủy</button>
          </div>
        </form>
        <p v-if="criterion.deadlineWarning" class="criterion-warning" :class="'criterion-' + criterion.deadlineWarning.level" role="status">
          {{ criterion.deadlineWarning.message }}
        </p>
        <template v-if="canPlan">
          <form v-if="editingDeadlineId === criterion.id" class="criteria-form" @submit.prevent="saveDeadline(criterion)">
            <label>Ngày hoàn thành <input v-model="deadlineDraft" type="date" required :disabled="!!pending"></label>
            <div class="criteria-actions">
              <button type="submit" :disabled="!!pending">Lưu deadline</button>
              <button type="button" :disabled="!!pending" @click="editingDeadlineId = ''">Hủy</button>
            </div>
          </form>
        </template>
        <p v-if="latest(criterion, 'REVIEW')?.outcome === 'full'" class="muted">AI bỏ qua đối chiếu đầu việc đã được GV xác nhận.</p>
        <p v-else>AI · {{ latest(criterion, 'AI') ? outcome('AI', latest(criterion, 'AI')!.outcome) : 'Chưa đối chiếu' }}
          <strong v-if="latest(criterion, 'AI')"> · {{ latest(criterion, 'AI')!.outcome === 'full' ? criterion.points : 0 }}/{{ criterion.points }} điểm theo báo cáo</strong>
        </p>
        <p v-if="latest(criterion, 'REVIEW')?.outcome !== 'full'">Giảng viên · {{ latest(criterion, 'REVIEW') ? outcome('REVIEW', latest(criterion, 'REVIEW')!.outcome) : 'Chưa điều chỉnh (dùng kết quả AI)' }}</p>
        <details v-if="criterion.history.length">
          <summary>Lịch sử và bằng chứng</summary>
          <div v-for="record in criterion.history" :key="record.id" class="criterion">
            <p>{{ record.source === 'AI' ? 'AI' : 'Giảng viên' }} · {{ outcome(record.source, record.outcome) }} ·
              {{ new Date(record.createdAt).toLocaleDateString('vi-VN') }}</p>
            <p v-if="record.isCurrent === false">Đánh giá đã được thay thế hoặc cho nội dung trước khi sửa.</p>
            <p v-if="record.git">{{ record.git.baseSha.slice(0, 8) }} → {{ record.git.headSha.slice(0, 8) }}</p>
            <p v-if="record.note">{{ record.note }}</p>
            <details v-if="record.evidence.length">
              <summary>Xem dòng Git</summary>
              <div v-for="line in record.evidence" :key="line.id">
                <p v-if="line.baseSha && line.headSha">{{ line.baseSha.slice(0, 8) }} → {{ line.headSha.slice(0, 8) }}</p>
                <p>{{ line.file }}</p><pre>{{ line.evidence }}</pre>
              </div>
            </details>
          </div>
        </details>
        <button v-if="canReview && latest(criterion, 'REVIEW')?.outcome === 'full'" type="button"
          :disabled="!!pending" :aria-expanded="!!reviewExpanded[criterion.id]" @click="reviewExpanded[criterion.id] = !reviewExpanded[criterion.id]">
          {{ reviewExpanded[criterion.id] ? 'Đóng điều chỉnh' : 'Điều chỉnh đánh giá' }}
        </button>
        <p v-if="reviewFeedback[criterion.id] && latest(criterion, 'REVIEW')?.outcome === 'full' && !reviewExpanded[criterion.id]"
          :role="reviewFeedback[criterion.id].type === 'error' ? 'alert' : 'status'" :class="'review-' + reviewFeedback[criterion.id].type">{{ reviewFeedback[criterion.id].message }}</p>
        <form v-if="canReview && (latest(criterion, 'REVIEW')?.outcome !== 'full' || reviewExpanded[criterion.id])" class="criteria-form" @submit.prevent>
          <p>Xác nhận hoàn thành sẽ tính toàn bộ trọng số đầu việc. Nếu AI chưa xác nhận đạt, giảng viên cần ghi căn cứ đã kiểm tra.</p>
          <label :for="`review-note-${criterion.id}`">Ghi chú hoặc tham chiếu bằng chứng
            <textarea :id="`review-note-${criterion.id}`" v-model.trim="notes[criterion.id]" maxlength="1000" rows="2"
              :disabled="!!pending" :aria-invalid="reviewFeedback[criterion.id]?.type === 'error'"
              :aria-describedby="reviewFeedback[criterion.id] ? `review-feedback-${criterion.id}` : undefined" />
          </label>
          <p v-if="reviewFeedback[criterion.id]" :id="`review-feedback-${criterion.id}`"
            :role="reviewFeedback[criterion.id].type === 'error' ? 'alert' : 'status'"
            :class="'review-' + reviewFeedback[criterion.id].type">{{ reviewFeedback[criterion.id].message }}</p>
          <div class="criteria-actions">
            <button type="button" :disabled="!!pending" @click="review(criterion, true, latest(criterion, 'AI')?.id ?? null)">{{ pending === criterion.id ? 'Đang lưu…' : 'Xác nhận hoàn thành' }}</button>
            <button type="button" :disabled="!!pending" @click="review(criterion, false, latest(criterion, 'AI')?.id ?? null)">Yêu cầu bổ sung</button>
          </div>
        </form>
      </article>
    </template>
  </section>
</template>

<style scoped>
.milestone-summary { background:#f0fdfa; border:1px solid #ccfbf1; border-radius:12px; padding:18px; margin:12px 0; }
.summary-heading,.criterion-heading { display:flex; align-items:center; justify-content:space-between; gap:12px; }
.summary-heading strong { font-size:1.8rem; color:#0f766e; }
.summary-stats,.criterion-meta { display:flex; flex-wrap:wrap; gap:8px 20px; font-size:.88rem; color:#64748b; margin-top:10px; }
progress { width:100%; height:10px; border:0; border-radius:20px; overflow:hidden; background:#e2e8f0; accent-color:#0f766e; margin:10px 0; }
progress::-webkit-progress-bar { background:#e2e8f0; }
progress::-webkit-progress-value { background:#0f766e; border-radius:20px; }
progress::-moz-progress-bar { background:#0f766e; }
.score-badge,.completion-badge { color:#166534; background:#dcfce7; border-radius:8px; padding:5px 10px; font-size:.88rem; font-weight:600; }
.score-badge { white-space:nowrap; color:#475569; background:#f1f5f9; }
.score-complete { color:#166534; background:#dcfce7; }
.milestone-state { display:inline-block; margin:6px 0; }
.calculation-help { color:#64748b; margin:12px 0 18px; font-size:.9rem; }
.criteria-panel summary { cursor:pointer; font-weight:600; padding:6px 0; }
.task-tools { margin:8px 0; }
.task-tools button { margin:5px 5px 0 0; }
.criteria-panel button { padding:8px 12px; border:1px solid #cbd5e1; border-radius:8px; background:#fff; color:#334155; cursor:pointer; }
.criteria-panel button:hover { background:#f1f5f9; }
.criteria-panel button:disabled { opacity:.55; cursor:not-allowed; }
.criteria-panel input,.criteria-panel textarea { box-sizing:border-box; padding:9px 10px; border:1px solid #cbd5e1; border-radius:8px; width:100%; font:inherit; }
.criteria-panel input[type=checkbox] { width:auto; }
.muted { color:#64748b; font-size:.88rem; }
.criterion.criterion-complete { border-left:4px solid #16a34a; }
@media(max-width:600px) { .criterion-heading { align-items:flex-start; flex-direction:column; } .criteria-actions button { flex:1 1 140px; } }

.check-status { margin: .8rem 0; padding: .7rem .85rem; border: 1px solid #cbd5e1; border-radius: 8px; }
.check-queued, .check-running { background: #eff6ff; border-color: #93c5fd; }
.check-completed { background: #f0fdf4; border-color: #86efac; }
.check-failed, .check-cancelled { background: #fff7ed; border-color: #fdba74; }
.check-spinner { display: inline-block; width: .85rem; height: .85rem; margin-right: .4rem; border: 2px solid #bfdbfe; border-top-color: #2563eb; border-radius: 50%; vertical-align: middle; animation: check-spin 1s linear infinite; }
@keyframes check-spin { to { transform: rotate(360deg); } }
@media (prefers-reduced-motion: reduce) { .check-spinner { animation: none; } }
.criteria-panel { margin-top: 1rem; padding-top: .75rem; border-top: 1px solid #cbd5e1; }
.criteria-panel :is(h4,h5,p) { margin: .35rem 0; }
.criteria-form { display: grid; gap: .55rem; margin: .7rem 0; justify-items: start; }
.criteria-form label { display: grid; gap: .3rem; width: min(100%, 42rem); }
.criteria-form :is(input,select,textarea) { max-width: 100%; }
.criteria-actions { display: flex; flex-wrap: wrap; gap: .4rem; }
.criterion { margin-top: .8rem; border: 1px solid #cbd5e1; border-radius: 8px; padding: .75rem; overflow-wrap: anywhere; }
.criterion pre { white-space: pre-wrap; overflow-wrap: anywhere; background: rgba(100,116,139,.09); padding: .5rem; }
.criterion-warning { padding: .65rem; border-radius: 6px; background: #fffbeb; color: #78350f; border: 1px solid #fbbf24; }
.criterion-danger { background: #fef2f2; color: #991b1b; border-color: #fca5a5; }
.review-error { color: #b91c1c; background: #fef2f2; border: 1px solid #fca5a5; border-radius: 6px; padding: .6rem; }
.review-success { color: #166534; background: #f0fdf4; border: 1px solid #86efac; border-radius: 6px; padding: .6rem; }
</style>
