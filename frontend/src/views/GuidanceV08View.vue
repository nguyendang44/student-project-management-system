<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HttpError } from '../api/client'
import { useAuthStore } from '../stores/auth'
import { v08Api, type ProjectOptionV08, type GuidanceV08, type GuidanceContextV08 } from '../features/v08/v08.api'
import { formatDateTimeV08 } from '../features/v08/v08DateTime'
const auth = useAuthStore()
const route = useRoute()
const projects = ref<ProjectOptionV08[]>([])
const projectId = ref('')
const entries = ref<GuidanceV08[]>([])
const context = ref<GuidanceContextV08 | null>(null)
const error = ref('')
const success = ref('')
const busy = ref(false)
const loading = ref(false)
const editingId = ref('')
const editingVersion = ref('')
const reviewingId = ref('')
const reviewVersion = ref('')
const taskVersion = ref('')
const taskEntryId = ref('')
const reviewDraft = ref({ feedback: '', nextActions: '' })
const taskDraft = ref({ milestoneId: '', title: '', acceptanceCriteria: '', points: 10, day: '' })
let loadSequence = 0
function meetingParts(value = new Date().toISOString()) {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
  const part = (type: string) => parts.find(p => p.type === type)?.value ?? ''
  return { day: `${part('year')}-${part('month')}-${part('day')}`, hour: part('hour'), minute: part('minute') }
}
const newForm = () => ({ ...meetingParts(), summary: '', questions: '', feedback: '', nextActions: '', milestoneId: '', gitProgressRunId: '' })
const form = ref(newForm())
const hours = Array.from({length:24}, (_,i) => String(i).padStart(2,'0'))
const minutes = Array.from({length:60}, (_,i) => String(i).padStart(2,'0'))
const selected = computed(() => projects.value.find(p => p.id === projectId.value))
const canWrite = computed(() => !!context.value?.canWrite && !loading.value && auth.role !== 'Admin')
const previousActions = computed(() => entries.value.find(e => e.status === 'CONFIRMED' && e.nextActions.trim()))
const fmt = formatDateTimeV08
const stateName = (status: string) => ({ PENDING: 'Chờ xác nhận', CHANGES_REQUESTED: 'Cần sửa', CONFIRMED: 'Đã xác nhận' }[status] ?? status)
const failure = (e: unknown) => e instanceof HttpError ? e.payload?.message ?? `API lỗi (${e.status})` : e instanceof Error ? e.message : 'Không thể xử lý yêu cầu.'
function resetForm() { form.value = newForm(); editingId.value = ''; editingVersion.value = '' }
async function loadProjects() {
  try {
    projects.value = await v08Api.projects()
    const incoming = typeof route.query.projectId === 'string' ? route.query.projectId : ''
    projectId.value = projects.value.some(p => p.id === incoming) ? incoming : projects.value[0]?.id ?? ''
  } catch (e) { error.value = failure(e) }
}
async function load() {
  const sequence = ++loadSequence, id = projectId.value
  if (!id) { entries.value = []; context.value = null; return }
  loading.value = true
  try {
    const [journal, options] = await Promise.all([v08Api.guidance(id), v08Api.guidanceContext(id)])
    if (sequence !== loadSequence || id !== projectId.value) return
    entries.value = journal; context.value = options; error.value = ''
    await focusNotifiedEntry()
  } catch (e) { if (sequence === loadSequence) error.value = failure(e) }
  finally { if (sequence === loadSequence) loading.value = false }
}
async function focusNotifiedEntry() {
  const id = typeof route.query.entryId === 'string' ? route.query.entryId : ''
  if (!id || !entries.value.some(entry => entry.id === id)) return
  await nextTick()
  document.getElementById('guidance-entry-' + id)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
async function save() {
  if (busy.value || !canWrite.value) return
  const f = form.value
  const instant = new Date(`${f.day}T${f.hour}:${f.minute}:00+07:00`)
  if (!f.summary.trim() || !Number.isFinite(instant.getTime()) || meetingParts(instant.toISOString()).day !== f.day) {
    error.value = 'Nhập ngày giờ hợp lệ và nội dung báo cáo.'; return
  }
  busy.value = true; error.value = ''; success.value = ''
  try {
    const body = { meetingAt: instant.toISOString(), summary: f.summary, questions: f.questions,
      milestoneId: f.milestoneId || null, gitProgressRunId: f.gitProgressRunId || null,
      ...(editingId.value ? { expectedUpdatedAt: editingVersion.value } : {}),
      ...(auth.role === 'Lecturer' ? { feedback: f.feedback, nextActions: f.nextActions } : {}) }
    if (editingId.value) await v08Api.updateGuidance(editingId.value, body)
    else await v08Api.createGuidance(projectId.value, body)
    resetForm(); success.value = 'Đã gửi nhật ký để giảng viên xem và xác nhận.'
    await load()
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
function edit(entry: GuidanceV08) {
  editingId.value = entry.id; editingVersion.value = entry.updatedAt
  form.value = { ...newForm(), ...meetingParts(entry.meetingAt), summary: entry.summary, questions: entry.questions,
    milestoneId: entry.milestoneId ?? '', gitProgressRunId: entry.gitProgressRunId ?? '' }
  document.getElementById('guidance-editor')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}
function openReview(entry: GuidanceV08) {
  reviewingId.value = entry.id; reviewVersion.value = entry.updatedAt
  reviewDraft.value = { feedback: entry.feedback, nextActions: entry.nextActions }
}
async function review(entry: GuidanceV08, confirm: boolean) {
  if (busy.value || !canWrite.value) return
  if (!confirm && !reviewDraft.value.feedback.trim()) { error.value = 'Nhập góp ý để sinh viên biết nội dung cần sửa.'; return }
  busy.value = true; error.value = ''; success.value = ''
  try {
    await v08Api.reviewGuidance(entry.id, { ...reviewDraft.value, confirm, expectedUpdatedAt: reviewVersion.value })
    reviewingId.value = ''; success.value = confirm ? 'Đã xác nhận buổi hướng dẫn. Không thay đổi điểm Milestone.' : 'Đã gửi yêu cầu sửa nhật ký cho sinh viên.'
    await load()
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
function openTask(entry: GuidanceV08) {
  taskEntryId.value = entry.id; taskVersion.value = entry.updatedAt
  taskDraft.value = { milestoneId: entry.milestoneId ?? '', title: entry.nextActions.trim().split('\n')[0].slice(0,200), acceptanceCriteria: entry.nextActions.trim().slice(0,250), points: 10, day: '' }
}
async function addTask(entry: GuidanceV08) {
  if (busy.value || !canWrite.value) return
  const f = taskDraft.value
  if (!f.milestoneId || !f.title.trim() || !f.acceptanceCriteria.trim()) { error.value = 'Chọn Milestone, nhập tên và điều kiện đạt.'; return }
  busy.value = true; error.value = ''; success.value = ''
  try {
    await v08Api.guidanceTask(entry.id, { milestoneId: f.milestoneId, title: f.title, acceptanceCriteria: f.acceptanceCriteria,
      points: f.points, deadlineAt: f.day ? new Date(`${f.day}T23:59:59.999+07:00`).toISOString() : null, expectedUpdatedAt: taskVersion.value })
    taskEntryId.value = ''; success.value = 'Đã thêm đầu việc vào Milestone. Đầu việc mới chưa có điểm hoàn thành.'
    await load()
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
watch(projectId, () => { resetForm(); reviewingId.value = ''; taskEntryId.value = ''; entries.value = []; context.value = null; error.value = ''; success.value = ''; void load() })
watch(() => route.query.projectId, id => { if (typeof id === 'string' && projects.value.some(p => p.id === id)) projectId.value = id })
watch(() => route.query.entryId, () => { void focusNotifiedEntry() })
onMounted(loadProjects)
</script>

<template>
  <section class="journal">
    <header><span class="eyebrow">ĐỒ ÁN · HƯỚNG DẪN</span><h2>Nhật ký hướng dẫn</h2><p class="muted">Chuẩn bị báo cáo, ghi nhận góp ý và theo dõi công việc sau mỗi buổi gặp.</p></header>
    <p v-if="error" role="alert" class="notice error">{{ error }}</p>
    <p v-if="success" role="status" class="notice success">{{ success }}</p>
    <div class="card">
      <p v-if="auth.role === 'Student' && projects.length === 1"><strong>Đồ án của bạn</strong><br>{{ selected?.topicTitle }} · {{ selected?.lecturerName }}</p>
      <label v-else>Chọn đồ án <select v-model="projectId" :disabled="busy"><option value="">Chọn đồ án</option><option v-for="p in projects" :key="p.id" :value="p.id">{{ p.topicTitle }} · {{ p.studentName }} · {{ p.lecturerName }}</option></select></label>
      <div v-if="entries.length" class="stats"><span>{{ entries.length }} buổi hướng dẫn đã tải</span><span>{{ entries.filter(e => e.status === 'PENDING').length }} chờ xác nhận</span><span>{{ entries.filter(e => e.status === 'CHANGES_REQUESTED').length }} cần sửa</span></div>
    </div>
    <p v-if="loading" role="status">Đang tải nhật ký và Milestone…</p>
    <aside v-if="previousActions" class="card carry-forward"><strong>Công việc từ buổi gần nhất · {{ fmt(previousActions.meetingAt) }}</strong><p class="multiline">{{ previousActions.nextActions }}</p>
      <RouterLink v-if="previousActions.nextTask" :to="{path:'/progress',query:{projectId,milestoneId:previousActions.nextTask.milestoneId}}">{{ previousActions.nextTask.isDeleted ? 'Đầu việc đã được xóa' : previousActions.nextTask.outcome === 'full' ? 'Đầu việc đã đạt' : 'Đầu việc cần tiếp tục' }} →</RouterLink>
    </aside>
    <form v-if="projectId && canWrite" id="guidance-editor" class="card editor" @submit.prevent="save">
      <div class="card-heading"><h3>{{ editingId ? 'Sửa và gửi lại nhật ký' : 'Chuẩn bị buổi hướng dẫn' }}</h3><span class="badge pending">{{ auth.role === 'Student' ? 'Sinh viên báo cáo' : 'Ghi nhận buổi gặp' }}</span></div>
      <div class="grid"><label>Ngày gặp <input type="date" required v-model="form.day" :disabled="busy"></label><fieldset class="time-field"><legend>Giờ gặp (24 giờ)</legend><div class="time-selects"><label>Giờ<select v-model="form.hour" :disabled="busy" required><option v-for="h in hours" :key="h" :value="h">{{h}}</option></select></label><label>Phút<select v-model="form.minute" :disabled="busy" required><option v-for="m in minutes" :key="m" :value="m">{{m}}</option></select></label></div></fieldset></div>
      <div class="grid"><label>Milestone liên quan (tùy chọn)<select v-model="form.milestoneId" :disabled="busy"><option value="">Chưa chọn</option><option v-for="m in context?.milestones" :key="m.id" :value="m.id">{{ m.title }}</option></select></label>
        <label>Báo cáo Git đính kèm (tùy chọn)<select v-model="form.gitProgressRunId" :disabled="busy"><option value="">Không đính kèm</option><option v-if="form.gitProgressRunId && !context?.reports.some(r => r.id === form.gitProgressRunId)" :value="form.gitProgressRunId">Báo cáo đã đính kèm trước đó</option><option v-for="r in context?.reports" :key="r.id" :value="r.id">{{ r.baseSha.slice(0,8) }} → {{ r.headSha.slice(0,8) }} · {{ r.completedAt ? fmt(r.completedAt) : r.modelName }}</option></select></label></div>
      <p class="muted">Báo cáo Git là tài liệu tham khảo. Đính kèm hoặc xác nhận nhật ký không tự cộng điểm tiến độ.</p>
      <label>Việc đã thực hiện <textarea required maxlength="4000" v-model="form.summary" rows="3" :disabled="busy" placeholder="Đã làm gì, kết quả và bằng chứng liên quan…" /></label>
      <label>Khó khăn và câu hỏi cần hỗ trợ <textarea maxlength="4000" v-model="form.questions" rows="2" :disabled="busy" placeholder="Vấn đề còn vướng hoặc nội dung cần trao đổi…" /></label>
      <template v-if="auth.role === 'Lecturer' && !editingId"><label>Góp ý của giảng viên <textarea maxlength="4000" v-model="form.feedback" rows="2" :disabled="busy" /></label><label>Việc giao cho lần tiếp theo <textarea maxlength="4000" v-model="form.nextActions" rows="2" :disabled="busy" /></label></template>
      <div class="actions"><button class="primary" :disabled="busy" type="submit">{{ busy ? 'Đang lưu…' : editingId ? 'Gửi lại giảng viên' : 'Gửi nhật ký' }}</button><button v-if="editingId" type="button" :disabled="busy" @click="resetForm">Hủy sửa</button></div>
    </form>
    <div class="section-heading"><h3>Lịch sử hướng dẫn</h3><button v-if="projectId" type="button" :disabled="busy || loading" @click="load">Làm mới</button></div>
    <p v-if="projectId && !entries.length && !loading" class="card muted">Chưa có nhật ký. Buổi gặp đầu tiên sẽ xuất hiện tại đây.</p>
    <article v-for="entry in entries" :key="entry.id" :id="'guidance-entry-' + entry.id" class="card entry" :class="{'focused':route.query.entryId === entry.id}">
      <div class="card-heading"><strong>{{ fmt(entry.meetingAt) }}</strong><span class="badge" :class="entry.status.toLowerCase()">{{ stateName(entry.status) }}</span></div>
      <div class="references"><RouterLink v-if="entry.milestoneId" :to="{path:'/progress',query:{projectId,milestoneId:entry.milestoneId}}">{{ entry.milestoneTitle ?? 'Xem Milestone' }}</RouterLink><RouterLink v-if="entry.git" :to="{path:'/git-progress',query:{projectId,runId:entry.git.id}}">Báo cáo Git {{ entry.git.baseSha.slice(0,8) }} → {{ entry.git.headSha.slice(0,8) }} ↗</RouterLink></div>
      <div class="entry-columns"><section><h4>Sinh viên báo cáo</h4><p class="multiline">{{ entry.summary }}</p><p v-if="entry.questions" class="multiline"><strong>Khó khăn / câu hỏi:</strong><br>{{ entry.questions }}</p></section>
        <section><h4>Giảng viên nhận xét</h4><p class="multiline">{{ entry.feedback || 'Chưa có nhận xét.' }}</p><p v-if="entry.nextActions" class="multiline"><strong>Việc tiếp theo:</strong><br>{{ entry.nextActions }}</p></section></div>
      <p v-if="entry.nextTask" class="task-link"><RouterLink :to="{path:'/progress',query:{projectId,milestoneId:entry.nextTask.milestoneId}}">{{ entry.nextTask.title }} · {{ entry.nextTask.isDeleted ? 'Đã xóa' : entry.nextTask.outcome === 'full' ? 'Đã đạt' : 'Chưa đạt' }} →</RouterLink></p>
      <div class="actions"><button v-if="canWrite && auth.role === 'Student' && entry.createdByUserId === auth.currentUser?.userId && entry.status !== 'CONFIRMED'" type="button" :disabled="busy" @click="edit(entry)">Sửa và gửi lại</button>
        <button v-if="canWrite && auth.role === 'Lecturer' && entry.status !== 'CONFIRMED' && reviewingId !== entry.id" class="primary" type="button" :disabled="busy" @click="openReview(entry)">Nhận xét và xác nhận</button>
        <button v-if="canWrite && auth.role === 'Lecturer' && entry.status === 'CONFIRMED' && entry.nextActions && !entry.nextTask && taskEntryId !== entry.id" type="button" :disabled="busy" @click="openTask(entry)">Thêm việc vào Milestone</button></div>
      <form v-if="canWrite && auth.role === 'Lecturer' && reviewingId === entry.id" class="review-form" @submit.prevent="review(entry,true)">
        <label>Nhận xét / lý do cần sửa <textarea v-model="reviewDraft.feedback" maxlength="4000" rows="3" :disabled="busy" /></label><label>Công việc cho lần tiếp theo <textarea v-model="reviewDraft.nextActions" maxlength="4000" rows="3" :disabled="busy" /></label>
        <div class="actions"><button class="primary" :disabled="busy" type="submit">Xác nhận buổi hướng dẫn</button><button type="button" :disabled="busy" @click="review(entry,false)">Yêu cầu sửa nhật ký</button><button type="button" :disabled="busy" @click="reviewingId = ''">Hủy</button></div>
      </form>
      <form v-if="canWrite && auth.role === 'Lecturer' && taskEntryId === entry.id" class="review-form" @submit.prevent="addTask(entry)">
        <h4>Chuyển việc được giao thành đầu việc</h4><p class="muted">Bản nháp được điền từ công việc đã giao. Hãy kiểm tra lại tên, điều kiện đạt và trọng số trước khi lưu; nội dung gốc vẫn được giữ trong nhật ký.</p>
        <label>Milestone <select v-model="taskDraft.milestoneId" required :disabled="busy"><option value="">Chọn Milestone</option><option v-for="m in context?.milestones" :key="m.id" :value="m.id">{{ m.title }}</option></select></label>
        <label>Tên đầu việc <input v-model.trim="taskDraft.title" required maxlength="200" :disabled="busy"></label><label>Điều kiện đạt <textarea v-model.trim="taskDraft.acceptanceCriteria" required maxlength="250" rows="2" :disabled="busy" /></label>
        <div class="grid"><label>Trọng số <input v-model.number="taskDraft.points" required type="number" min="1" max="100" :disabled="busy"></label><label>Deadline (có thể đặt sau) <input v-model="taskDraft.day" type="date" :disabled="busy"></label></div>
        <div class="actions"><button class="primary" :disabled="busy" type="submit">Lưu đầu việc</button><button type="button" :disabled="busy" @click="taskEntryId = ''">Hủy</button></div>
      </form>
    </article>
  </section>
</template>

<style scoped>
.journal { max-width:1120px; margin:0 auto; color:#1e293b; }
.journal h2 { margin:8px 0; font-size:1.8rem; }.journal h3,.journal h4 { margin:0 0 10px; }
.eyebrow { font-size:.72rem; letter-spacing:.12em; color:#0f766e; font-weight:700; }
.card { background:#fff; border:1px solid #e2e8f0; border-radius:14px; padding:22px; margin:18px 0; }
.editor,.review-form { display:grid; gap:14px; scroll-margin-top:80px; }
.review-form { background:#f8fafc; border-top:1px solid #e2e8f0; padding-top:18px; margin-top:18px; }
.time-field { margin:0; padding:0; border:0; min-width:0; }.time-field legend { padding:0 0 6px; font-size:.92rem; font-weight:600; }.time-selects { display:grid; grid-template-columns:1fr 1fr; gap:10px; }
.grid,.entry-columns { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; }
.entry-columns { margin:18px 0; }.entry-columns section { min-width:0; }
.journal label { display:grid; gap:6px; font-size:.92rem; font-weight:600; }
.journal :is(input,select,textarea) { width:100%; box-sizing:border-box; min-width:0; border:1px solid #cbd5e1; padding:10px 12px; border-radius:8px; background:#fff; font:inherit; color:inherit; }
.journal textarea { resize:vertical; }.journal button { cursor:pointer; border:1px solid #cbd5e1; border-radius:8px; padding:9px 14px; background:#fff; color:#334155; }
.journal button.primary { background:#0f766e; color:white; border-color:#0f766e; }.journal button:disabled { opacity:.55; cursor:not-allowed; }
.actions,.references,.stats { display:flex; flex-wrap:wrap; gap:10px 18px; }.references { margin-top:12px; font-size:.88rem; }
.stats { font-size:.85rem; color:#64748b; margin-top:12px; }.card-heading,.section-heading { display:flex; align-items:center; justify-content:space-between; gap:12px; flex-wrap:wrap; }
.badge { display:inline-block; font-size:.78rem; font-weight:600; border-radius:20px; padding:5px 10px; }.pending { color:#92400e; background:#fef3c7; }.confirmed { color:#166534; background:#dcfce7; }.changes_requested { color:#9f1239; background:#ffe4e6; }
.multiline { white-space:pre-wrap; overflow-wrap:anywhere; margin:8px 0; line-height:1.65; }.muted { color:#64748b; font-size:.9rem; line-height:1.6; }
.notice { padding:12px 16px; border-radius:8px; }.error { color:#991b1b; background:#fef2f2; }.success { color:#166534; background:#f0fdf4; }
.carry-forward { background:#f0fdfa; border-color:#99f6e4; }.task-link { border-top:1px solid #e2e8f0; padding-top:12px; }.focused { outline:2px solid #0d9488; scroll-margin-top:80px; }
@media(max-width:640px) { .grid,.entry-columns { grid-template-columns:1fr; }.card { padding:16px; }.actions button { flex:1 1 160px; } }
</style>
