<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { milestoneCriteriaApi, type MilestoneCriterion, type CriterionPlanDraft, type CriterionWrite } from '../features/progress/milestoneCriteria.api'

const props = defineProps<{ milestoneId: string; canPlan: boolean; canAssess: boolean; canReview: boolean }>()
const emit = defineEmits<{ changed: [] }>()
const details = ref<Awaited<ReturnType<typeof milestoneCriteriaApi.details>> | null>(null)
const points = computed(() => details.value?.criteria.reduce((sum, criterion) => sum + criterion.points, 0) ?? 0)
const form = ref({ title: '', acceptanceCriteria: '', points: 10 })
const newDeadline = ref('')
const editingDeadlineId = ref('')
const deadlineDraft = ref('')
const formOpen = ref(false)
const selectedReport = ref('')
const notes = ref<Record<string, string>>({})
const pending = ref('')
const planDraft = ref<(CriterionPlanDraft & { key: number; checked: boolean; day: string })[]>([])
const editingId = ref('')
const editForm = ref({ title: '', acceptanceCriteria: '', points: 10, day: '' })
const error = ref('')
const info = ref('')
const lifetime = new AbortController()

function errorText(e: unknown): string { return e instanceof Error ? e.message : 'Có lỗi khi cập nhật đầu việc.' }
async function reload() {
  details.value = await milestoneCriteriaApi.details(props.milestoneId, lifetime.signal)
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
function propose() {
  void perform('ai', () => milestoneCriteriaApi.propose(props.milestoneId, selectedReport.value, lifetime.signal),
    'Đã cập nhật đề xuất AI cho báo cáo Git đã chọn.')
}
function review(criterion: MilestoneCriterion, isMet: boolean, proposalId: string | null) {
  void perform(criterion.id, async () => {
    await milestoneCriteriaApi.review(props.milestoneId, criterion.id, isMet, proposalId, notes.value[criterion.id] ?? '')
    delete notes.value[criterion.id]
  }, isMet ? 'Đã xác nhận đầu việc đạt.' : 'Đã lưu nhận xét cần bổ sung.')
}
const latest = (criterion: MilestoneCriterion, source: 'AI' | 'REVIEW') =>
  criterion.history.find(assessment => assessment.source === source && assessment.isCurrent !== false)
const outcome = (source: 'AI' | 'REVIEW', value: string) => source === 'REVIEW'
  ? (value === 'full' ? 'Đã xác nhận đạt' : 'Cần bổ sung')
  : ({ full: 'Có bằng chứng', partial: 'Có bằng chứng một phần', missing: 'Chưa đủ dữ liệu' }[value] ?? value)
onMounted(async () => {
  try { await reload() }
  catch (e) { if (!lifetime.signal.aborted) error.value = errorText(e) }
})
onUnmounted(() => lifetime.abort())
</script>

<template>
  <section class="criteria-panel" aria-label="Đầu việc và bằng chứng">
    <h4>Đầu việc và bằng chứng</h4>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="info" role="status">{{ info }}</p>
    <template v-if="details">
      <p>Tổng trọng số <strong>{{ points }}</strong></p>
      <p v-if="points">Tiến độ = trọng số đầu việc đã đạt / {{ points }} × 100%.</p>
      <p v-if="details.proposedPercent != null">Tiến độ Milestone theo AI <strong>{{ details.proposedPercent }}%</strong></p>
      <p v-if="details.criteria.length">AI đối chiếu báo cáo Git đã chọn. Mục đủ bằng chứng được tính trọng số. Mục có bằng chứng một phần hoặc thiếu bằng chứng được tính 0 điểm đề xuất.</p>
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
      <form v-if="canAssess && details.criteria.length" class="criteria-form" @submit.prevent="propose">
        <label>Báo cáo Git đã hoàn tất của đồ án
          <select v-model="selectedReport" :disabled="!!pending">
            <option value="">Chọn cặp commit để đối chiếu</option>
            <option v-for="report in details.reports" :key="report.id" :value="report.id">
              {{ report.baseSha.slice(0, 8) }} → {{ report.headSha.slice(0, 8) }} · {{ report.repositoryUrl }}
            </option>
          </select>
        </label>
        <button type="submit" :disabled="!selectedReport || !!pending">
          {{ pending === 'ai' ? 'Đang đối chiếu bằng chứng…' : 'AI đề xuất tiến độ' }}
        </button>
        <p v-if="!details.reports.length"><RouterLink to="/git-progress">Tạo báo cáo tiến độ Git</RouterLink></p>
      </form>
      <article v-for="criterion in details.criteria" :key="criterion.id" class="criterion">
        <h5>{{ criterion.title }} · Trọng số {{ criterion.points }}</h5>
        <p>Chiếm {{ criterion.percentOfMilestone }}% Milestone</p>
        <p>{{ criterion.acceptanceCriteria }}</p>
        <div v-if="canPlan" class="criteria-actions">
          <button type="button" :disabled="!!pending" @click="edit(criterion)">Sửa đầu việc</button>
          <button type="button" :disabled="!!pending" @click="remove(criterion)">Xóa đầu việc</button>
        </div>
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
        <p>Deadline đầu việc · <strong>{{ criterion.deadlineAt ? dateText(criterion.deadlineAt) : 'Chưa đặt' }}</strong></p>
        <template v-if="canPlan">
          <button v-if="editingDeadlineId !== criterion.id" type="button" :disabled="!!pending"
            @click="editDeadline(criterion)">Đặt hoặc sửa deadline đầu việc</button>
          <form v-else class="criteria-form" @submit.prevent="saveDeadline(criterion)">
            <label>Ngày hoàn thành <input v-model="deadlineDraft" type="date" required :disabled="!!pending"></label>
            <div class="criteria-actions">
              <button type="submit" :disabled="!!pending">Lưu deadline</button>
              <button type="button" :disabled="!!pending" @click="editingDeadlineId = ''">Hủy</button>
            </div>
          </form>
        </template>
        <p>AI · {{ latest(criterion, 'AI') ? outcome('AI', latest(criterion, 'AI')!.outcome) : 'Chưa đối chiếu' }}
          <strong v-if="latest(criterion, 'AI')"> · {{ latest(criterion, 'AI')!.outcome === 'full' ? criterion.points : 0 }}/{{ criterion.points }} điểm theo báo cáo</strong>
        </p>
        <p>Giảng viên · {{ latest(criterion, 'REVIEW') ? outcome('REVIEW', latest(criterion, 'REVIEW')!.outcome) : 'Chờ xác nhận' }}</p>
        <details v-if="criterion.history.length">
          <summary>Lịch sử và bằng chứng</summary>
          <div v-for="record in criterion.history" :key="record.id" class="criterion">
            <p>{{ record.source === 'AI' ? 'AI' : 'Giảng viên' }} · {{ outcome(record.source, record.outcome) }} ·
              {{ new Date(record.createdAt).toLocaleDateString('vi-VN') }}</p>
            <p v-if="record.isCurrent === false">Đánh giá cho nội dung trước khi sửa.</p>
            <p v-if="record.git">{{ record.git.baseSha.slice(0, 8) }} → {{ record.git.headSha.slice(0, 8) }}</p>
            <p v-if="record.note">{{ record.note }}</p>
            <details v-if="record.evidence.length">
              <summary>Xem dòng Git</summary>
              <div v-for="line in record.evidence" :key="line.id">
                <p>{{ line.file }}</p><pre>{{ line.evidence }}</pre>
              </div>
            </details>
          </div>
        </details>
        <form v-if="canReview" class="criteria-form" @submit.prevent>
          <label>Ghi chú hoặc tham chiếu bằng chứng
            <textarea v-model.trim="notes[criterion.id]" maxlength="1000" rows="2" />
          </label>
          <div class="criteria-actions">
            <button type="button" :disabled="!!pending" @click="review(criterion, true, latest(criterion, 'AI')?.id ?? null)">Xác nhận đạt</button>
            <button type="button" :disabled="!!pending" @click="review(criterion, false, latest(criterion, 'AI')?.id ?? null)">Yêu cầu bổ sung</button>
          </div>
        </form>
      </article>
    </template>
  </section>
</template>

<style scoped>
.criteria-panel { margin-top: 1rem; padding-top: .75rem; border-top: 1px solid #cbd5e1; }
.criteria-panel :is(h4,h5,p) { margin: .35rem 0; }
.criteria-form { display: grid; gap: .55rem; margin: .7rem 0; justify-items: start; }
.criteria-form label { display: grid; gap: .3rem; width: min(100%, 42rem); }
.criteria-form :is(input,select,textarea) { max-width: 100%; }
.criteria-actions { display: flex; flex-wrap: wrap; gap: .4rem; }
.criterion { margin-top: .8rem; border: 1px solid #cbd5e1; border-radius: 8px; padding: .75rem; overflow-wrap: anywhere; }
.criterion pre { white-space: pre-wrap; overflow-wrap: anywhere; background: rgba(100,116,139,.09); padding: .5rem; }
</style>
