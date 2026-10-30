<script setup lang="ts">
// v0.7.8: editable 8x6 Milestone templates.
import MilestoneTemplatePlanner from '../components/MilestoneTemplatePlanner.vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { HttpError } from '../api/client'
import { sortMilestonesForDisplay } from '../features/progress/sortMilestones'
// V0712_SORT_MILESTONES
import { projectProgressApi, type ProjectV07, type MilestoneV07, type MilestoneForm, type ProgressHistory } from '../features/progress/projectProgress.api'

const props = defineProps<{ moduleId: string }>()
const route = useRoute() // V0804_NOTIFICATION_LINKS
const auth = useAuthStore()
const role = computed(() => auth.role)
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const milestones = ref<MilestoneV07[]>([])
const selectedId = ref('')
const history = ref<ProgressHistory | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const success = ref('')
const editing = ref(false)
const showMilestoneForm = ref(false)
// V078_MILESTONE_TEMPLATE_LIBRARY
const showTemplatePlanner = ref(false)
const emptyMilestone = (): MilestoneForm => ({title:'', description:'', startAt:'', deadlineAt:'', expectedResult:''})
const milestoneForm = ref<MilestoneForm>(emptyMilestone())
// V095_DEADLINE_CHANGE: independent date-only editor, no repeated approval notice while pending.
const deadlineEditingId = ref('')
const deadlineDraft = ref('')
const canStudentEditDeadline = (m: MilestoneV07) =>
  role.value === 'Student' && !!canWork.value && !['SUBMITTED','APPROVED'].includes(m.status)
const percent = ref(0)
const workDone = ref('')
const note = ref('')
const resultSummary = ref('')
const documentReference = ref('')
const feedback = ref('')
const selectedProject = computed(() => projects.value.find(x => x.id === projectId.value))
const selected = computed(() => milestones.value.find(x => x.id === selectedId.value))
const canWork = computed(() => selectedProject.value && ['REGISTERED','IN_PROGRESS','DRAFT'].includes(selectedProject.value.status))
const canAddMilestone = computed(() => selectedProject.value && ['REGISTERED','IN_PROGRESS','DRAFT','COMPLETED'].includes(selectedProject.value.status))
const canStudentUpdate = computed(() => role.value === 'Student' && canWork.value && selected.value && selected.value.proposalStatus === 'CONFIRMED' && !['SUBMITTED','APPROVED'].includes(selected.value.status))
const canStudentSubmit = computed(() => role.value === 'Student' && canWork.value && selected.value && selected.value.proposalStatus === 'CONFIRMED' && selected.value.status !== 'APPROVED')
const canStudentEditProposal = computed(() => role.value === 'Student' && canWork.value && selected.value?.proposedByStudent && selected.value.proposalStatus === 'CHANGES_REQUESTED')
const canLecturerReviewProposal = computed(() => role.value === 'Lecturer' && canWork.value && selected.value?.proposalStatus === 'PENDING_REVIEW')
const canLecturerReview = computed(() => role.value === 'Lecturer' && canWork.value && selected.value?.status === 'SUBMITTED')
const canLecturerEdit = computed(() => role.value === 'Lecturer' && canWork.value && selected.value && !['SUBMITTED','APPROVED'].includes(selected.value.status))
const isMilestonesPage = computed(() => props.moduleId === 'milestones')
const isProgressPage = computed(() => props.moduleId === 'progress')
const isSubmissionsPage = computed(() => props.moduleId === 'submissions')
const isEvaluationsPage = computed(() => props.moduleId === 'evaluations')
const sectionTitle = computed(() => ({milestones:'Quản lý Milestone',progress:'Cập nhật tiến độ',submissions:'Nộp kết quả',evaluations:'Đánh giá Milestone'}[props.moduleId] || 'Tiến độ dự án'))
const sectionDescription = computed(() => ({
  milestones:'Danh sách mốc công việc của Project và quy trình giảng viên xác nhận kế hoạch Milestone.',
  progress:'Cập nhật phần trăm hoàn thành và nội dung công việc đã thực hiện theo Milestone.',
  submissions:'Gửi kết quả và tài liệu thực hiện Milestone cho giảng viên.',
  evaluations:'Giảng viên xem bản nộp, nhận xét, yêu cầu sửa hoặc phê duyệt kết quả Milestone.'
}[props.moduleId] || '') )
function errorText(e: unknown) {
  if (e instanceof HttpError) return e.payload?.message || `API lỗi (${e.status})`
  return e instanceof Error ? e.message : 'Không thể xử lý yêu cầu.'
}
// V075_DEADLINE_DATE_ONLY
// Date-only deadline is a Vietnamese civil day, independent of OS/browser timezone.
const deadlineDate = (date: string) => {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone:'Asia/Ho_Chi_Minh',
    year:'numeric', month:'2-digit', day:'2-digit' }).formatToParts(new Date(date))
  const part = (type: string) => parts.find(p => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}
const readableDeadline = (date: string) => new Intl.DateTimeFormat('vi-VN', {
  timeZone:'Asia/Ho_Chi_Minh', day:'2-digit', month:'2-digit', year:'numeric'
}).format(new Date(date))
const readableDate = (date: string) => new Date(date).toLocaleString('vi-VN', { day:'2-digit', month:'2-digit', year:'numeric', hour:'2-digit', minute:'2-digit', hour12:false, hourCycle:'h23' })
const statusName = (status: string) => ({PENDING:'Chưa bắt đầu', IN_PROGRESS:'Đang thực hiện', SUBMITTED:'Chờ giảng viên duyệt', REVISION_REQUIRED:'Cần chỉnh sửa', APPROVED:'Đã duyệt', OVERDUE:'Quá hạn', REGISTERED:'Đã đăng ký',COMPLETED:'Hoàn thành',CANCELLED:'Đã hủy'}[status] || status)
async function loadProjects() {
  loading.value = true; error.value = ''
  try {
    projects.value = await projectProgressApi.projects()
    const incoming = typeof route.query.projectId === 'string' ? route.query.projectId : ''
    if (!projects.value.some(p => p.id === projectId.value)) projectId.value = projects.value.some(p => p.id === incoming) ? incoming : (projects.value[0]?.id || '')
    await loadMilestones()
  } catch (e) { error.value = errorText(e) }
  finally { loading.value = false }
}
async function loadMilestones() {
  deadlineEditingId.value = ''; deadlineDraft.value = ''
  showTemplatePlanner.value = false
  error.value = ''; success.value = ''; showMilestoneForm.value = false
  if (!projectId.value) { milestones.value = []; selectedId.value = ''; history.value = null; return }
  try {
    milestones.value = sortMilestonesForDisplay(await projectProgressApi.milestones(projectId.value))
    const requestedMilestone = typeof route.query.milestoneId === 'string' ? route.query.milestoneId : ''
    if (requestedMilestone && milestones.value.some(x => x.id === requestedMilestone)) selectedId.value = requestedMilestone
    else if (!milestones.value.some(x => x.id === selectedId.value)) selectedId.value = milestones.value[0]?.id || ''
    await loadHistory()
  } catch(e) { error.value = errorText(e) }
}
async function loadHistory() {
  history.value = null
  if (!selectedId.value) return
  try { history.value = await projectProgressApi.history(selectedId.value) }
  catch(e) { error.value = errorText(e) }
  percent.value = selected.value?.percentComplete ?? 0
  workDone.value = ''; note.value = ''; feedback.value = ''; proposalFeedback.value = ''
  const latestSubmission = history.value?.submissions[history.value.submissions.length - 1]
  resultSummary.value = latestSubmission?.resultSummary ?? ''
  documentReference.value = latestSubmission?.documentReference ?? ''
}
async function run(action: () => Promise<unknown>, message: string) {
  saving.value = true; error.value = ''; success.value = ''
  try {
    await action()
    await loadProjects()
    success.value = message
  } catch(e) { error.value = errorText(e) }
  finally { saving.value = false }
}
const proposalFeedback = ref('')
const proposalStatusName = (status: string) => ({PENDING_REVIEW:'Chờ giảng viên xem xét kế hoạch',CHANGES_REQUESTED:'Giảng viên yêu cầu sửa kế hoạch',CONFIRMED:'Kế hoạch đã xác nhận'}[status] || status)
function reviewProposal(isConfirmed: boolean) {
  if (!selected.value || !canLecturerReviewProposal.value) return
  if (!isConfirmed && !proposalFeedback.value.trim()) { error.value = 'Cần nhận xét khi yêu cầu sinh viên sửa kế hoạch.'; return }
  void run(() => projectProgressApi.proposalReview(selectedId.value,isConfirmed,proposalFeedback.value),
    isConfirmed ? 'Đã xác nhận kế hoạch Milestone và chuyển cho sinh viên thực hiện.' : 'Đã gửi góp ý để sinh viên sửa kế hoạch.')
}
async function onTemplatesApplied(count: number) {
  showTemplatePlanner.value = false
  await loadProjects()
  success.value = role.value === 'Student'
    ? `Đã gửi ${count} Milestone đề xuất để giảng viên xem xét.`
    : `Đã tạo ${count} Milestone từ mẫu.`
}
function newMilestone() {
  showTemplatePlanner.value = false
  editing.value = false; milestoneForm.value = emptyMilestone(); showMilestoneForm.value = true
}
function editMilestone() {
  showTemplatePlanner.value = false
  if (!selected.value) return
  editing.value = true
  milestoneForm.value = {title:selected.value.title,description:selected.value.description || '',expectedResult:selected.value.expectedResult || '',
    startAt:selected.value.startAt,deadlineAt:deadlineDate(selected.value.deadlineAt)}
  showMilestoneForm.value = true
}
async function saveMilestone() {
  if (!projectId.value) return
  const day = milestoneForm.value.deadlineAt
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) { error.value = 'Vui lòng chọn ngày Deadline.'; return }
  // Date input has no timezone. Interpret the chosen day in Vietnam (+07:00).
  const deadline = new Date(`${day}T23:59:59.999+07:00`)
  const midnight = new Date(`${day}T00:00:00+07:00`)
  if (!Number.isFinite(deadline.getTime()) || !Number.isFinite(midnight.getTime())) {
    error.value = 'Ngày Deadline không hợp lệ.'; return
  }
  // StartAt remains an internal backward-compatible API field, never a required UI input.
  const oldStart = editing.value && selected.value ? new Date(selected.value.startAt) : midnight
  const start = Number.isFinite(oldStart.getTime()) && oldStart <= deadline ? oldStart : midnight
  const request: MilestoneForm = { ...milestoneForm.value,
    startAt:start.toISOString(), deadlineAt:deadline.toISOString() }
  const wasEditing = editing.value
  const id = selectedId.value
  await run(() => wasEditing ? projectProgressApi.update(id,request) : projectProgressApi.create(projectId.value,request),
    wasEditing
      ? (role.value === 'Student' ? 'Đã sửa và gửi lại kế hoạch cho giảng viên.' : 'Đã cập nhật Milestone.')
      : (role.value === 'Student' ? 'Đã gửi đề xuất Milestone cho giảng viên.' : 'Đã tạo Milestone.'))
}
function beginDeadlineEdit(m: MilestoneV07) {
  if (!canStudentEditDeadline(m)) return
  deadlineEditingId.value = m.id
  deadlineDraft.value = deadlineDate(m.deadlineAt)
  showMilestoneForm.value = false
}
async function saveDeadline(m: MilestoneV07) {
  if (!canStudentEditDeadline(m) || deadlineEditingId.value !== m.id || saving.value) return
  const day = deadlineDraft.value
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) { error.value = 'Vui lòng chọn ngày nộp hợp lệ.'; return }
  const deadline = new Date(`${day}T23:59:59.999+07:00`)
  if (!Number.isFinite(deadline.getTime()) || deadlineDate(deadline.toISOString()) !== day) {
    error.value = 'Ngày nộp không hợp lệ.'; return
  }
  saving.value = true; error.value = ''; success.value = ''
  try {
    const result = await projectProgressApi.updateDeadline(m.id, deadline.toISOString())
    deadlineEditingId.value = ''; deadlineDraft.value = ''
    await loadProjects()
    success.value = !result.changed
      ? 'Ngày nộp không thay đổi, không gửi thông báo mới.'
      : result.requiresReapproval
        ? 'Đã đề xuất ngày nộp mới. Milestone chờ giảng viên xác nhận lại và giảng viên nhận thông báo.'
        : m.proposalStatus === 'CHANGES_REQUESTED'
          ? 'Đã đổi ngày nộp. Kế hoạch vẫn cần sửa theo góp ý và gửi lại khi hoàn tất; chưa gửi thông báo mới.'
          : 'Đã đổi ngày nộp. Kế hoạch vẫn đang chờ xác nhận, không gửi thông báo mới.'
  } catch(e) { error.value = errorText(e) }
  finally { saving.value = false }
}
function submitProgress() {
  if (!selected.value) return
  void run(() => projectProgressApi.progress(selectedId.value, percent.value, workDone.value, note.value), 'Đã cập nhật tiến độ.')
}
function submitResult() {
  if (!selected.value || !canStudentSubmit.value) return
  const resubmitting = selected.value.status === 'SUBMITTED'
  void run(() => projectProgressApi.submit(selectedId.value,resultSummary.value,documentReference.value),
    resubmitting ? 'Đã nộp lại kết quả; giảng viên sẽ xem bản mới nhất.' : 'Đã nộp kết quả cho giảng viên.')
}
function review(isApproved: boolean) {
  if (!selected.value) return
  if (!isApproved && !feedback.value.trim()) { error.value = 'Cần nhập nhận xét khi yêu cầu sinh viên sửa.'; return }
  void run(() => projectProgressApi.review(selectedId.value,isApproved,feedback.value), isApproved ? 'Đã phê duyệt Milestone.' : 'Đã gửi yêu cầu chỉnh sửa.')
}
watch(projectId, () => { void loadMilestones() })
watch(selectedId, () => { showMilestoneForm.value = false; deadlineEditingId.value = ''; deadlineDraft.value = ''; void loadHistory() })
watch(() => route.query.milestoneId, (id) => {
  if (typeof id === 'string' && milestones.value.some(m => m.id === id)) selectedId.value = id
})
watch(() => route.query.projectId, (id) => {
  if (typeof id === 'string' && projects.value.some(p => p.id === id)) projectId.value = id
})
onMounted(() => { void loadProjects() })
</script>

<template>
  <section class="intro">
    <span class="section-chip">Dự án · v0.7</span>
    <h2>{{sectionTitle}}</h2>
    <p>{{sectionDescription}}</p>
  </section>
  <p v-if="error" class="v07-notice v07-error" role="alert">{{error}}</p>
  <p v-if="success" class="v07-notice v07-success" role="status">{{success}}</p>

  <article class="panel v07-block">
    <!-- V076_STUDENT_SINGLE_PROJECT_AUTO_SELECT -->
    <h3>{{role === 'Student' && projects.length === 1 ? 'Project của bạn' : 'Chọn Project'}}</h3>
    <p v-if="loading">Đang tải dữ liệu…</p>
    <p v-else-if="role === 'Student' && projects.length === 1" class="v07-single-project">
      <strong>{{projects[0].topicTitle}}</strong> · {{projects[0].studentName}} · {{projects[0].lecturerName}}
    </p>
    <label v-else>Project của {{role === 'Student' ? 'bạn' : role === 'Lecturer' ? 'sinh viên bạn hướng dẫn' : 'hệ thống'}}
      <select v-model="projectId" :disabled="saving">
        <option v-if="!projects.length" value="">Chưa có Project</option>
        <option v-for="p in projects" :key="p.id" :value="p.id">{{p.topicTitle}} · {{p.studentName}} · {{p.lecturerName}}</option>
      </select>
    </label>
    <div v-if="selectedProject" class="v07-meta">
      <span>Trạng thái: <strong>{{statusName(selectedProject.status)}}</strong></span>
      <span>Milestone đã duyệt: <strong>{{selectedProject.approvedCount}}/{{selectedProject.milestoneCount}}</strong></span>
      <span>Tiến độ tổng hợp: <strong>{{selectedProject.percentComplete}}%</strong></span>
    </div>
  </article>

  <!-- Milestone catalogue and plan approvals, without progress/submission forms. -->
  <article v-if="selectedProject && isMilestonesPage" class="panel v07-block">
    <div class="v07-head">
      <h3>Milestones của Project</h3>
      <button v-if="['Lecturer','Student'].includes(role || '') && canAddMilestone" type="button" @click="newMilestone">{{role === 'Student' ? '+ Đề xuất Milestone' : '+ Tạo Milestone'}}</button>
      <button v-if="['Lecturer','Student'].includes(role || '') && canAddMilestone" type="button" @click="showTemplatePlanner = !showTemplatePlanner; showMilestoneForm = false">{{role === 'Student' ? '+ Đề xuất theo mẫu' : '+ Tạo từ mẫu'}}</button>
    </div>
    <div class="table-scroller"><table class="v07-table">
      <thead><tr><th>Milestone</th><th>Deadline</th><th>Tiến độ</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
      <tbody>
        <tr v-for="m in milestones" :key="m.id">
          <td><strong>{{m.title}}</strong><p class="v07-muted">{{m.description}}</p></td>
          <td>{{readableDeadline(m.deadlineAt)}}</td><td>{{m.percentComplete}}%</td>
          <td><span v-if="m.proposalStatus !== 'CONFIRMED'">{{proposalStatusName(m.proposalStatus)}}</span><span v-else>{{statusName(m.status)}}</span><strong v-if="m.isOverdue" class="v07-late"> · Quá hạn</strong></td>
          <td><button type="button" :disabled="saving" @click="selectedId = m.id">{{selectedId === m.id ? 'Đang xem' : 'Chi tiết'}}</button></td>
        </tr>
        <tr v-if="!milestones.length"><td colspan="5" class="empty-cell">Chưa có Milestone. Giảng viên có thể tạo mốc hoặc sinh viên đề xuất kế hoạch mới.</td></tr>
      </tbody>
    </table></div>
    <MilestoneTemplatePlanner v-if="showTemplatePlanner && canAddMilestone && ['Lecturer','Student'].includes(role || '')"
      :project-id="projectId" :role="role === 'Student' ? 'Student' : 'Lecturer'"
      :existing-titles="milestones.map(m => m.title)"
      @applied="onTemplatesApplied" @close="showTemplatePlanner = false" />
    <form v-if="['Lecturer','Student'].includes(role || '') && showMilestoneForm" class="v07-form" @submit.prevent="saveMilestone">
      <h4>{{editing ? (role === 'Student' ? 'Sửa kế hoạch theo góp ý giảng viên' : 'Chỉnh sửa Milestone') : (role === 'Student' ? 'Đề xuất Milestone gửi giảng viên' : 'Tạo Milestone')}}</h4>
      <label>Tên Milestone <input v-model.trim="milestoneForm.title" required maxlength="300" /></label>
      <label>Mô tả <textarea v-model.trim="milestoneForm.description" maxlength="4000" rows="2" /></label>
      <label>Deadline (ngày cuối cùng) <input v-model="milestoneForm.deadlineAt" type="date" required /></label>
      <p class="v07-muted">Hạn nộp được tính đến hết ngày đã chọn (giờ Việt Nam).</p>
      <label>Kết quả cần đạt <textarea v-model.trim="milestoneForm.expectedResult" maxlength="2000" rows="2" /></label>
      <div class="v07-actions">
        <button :disabled="saving" type="submit">{{role === 'Student' ? (editing ? 'Sửa và gửi lại giảng viên' : 'Gửi kế hoạch cho giảng viên') : (editing ? 'Lưu Milestone' : 'Tạo Milestone')}}</button>
        <button type="button" :disabled="saving" @click="showMilestoneForm = false">Hủy</button>
      </div>
    </form>
  </article>

  <!-- Progress, submission and evaluation pages use one compact Milestone selector. -->
  <article v-if="selectedProject && !isMilestonesPage" class="panel v07-block">
    <h3>Chọn Milestone</h3>
    <label>Milestone của Project
      <select v-model="selectedId" :disabled="saving">
        <option v-if="!milestones.length" value="">Chưa có Milestone</option>
        <option v-for="m in milestones" :key="m.id" :value="m.id">{{m.title}} · {{m.proposalStatus !== 'CONFIRMED' ? proposalStatusName(m.proposalStatus) : statusName(m.status)}}</option>
      </select>
    </label>
    <div v-if="selected" class="v07-meta">
      <span>Deadline: <strong>{{readableDeadline(selected.deadlineAt)}}</strong></span>
      <span>Tiến độ: <strong>{{selected.percentComplete}}%</strong></span>
      <span>Trạng thái: <strong>{{selected.proposalStatus !== 'CONFIRMED' ? proposalStatusName(selected.proposalStatus) : statusName(selected.status)}}</strong></span>
    </div>
  </article>

  <!-- Milestone details and lecturer approval of a student-proposed plan. -->
  <article v-if="selected && isMilestonesPage" class="panel v07-block">
    <div class="v07-head"><h3>Chi tiết: {{selected.title}}</h3>
      <button v-if="canLecturerEdit || canStudentEditProposal" type="button" @click="editMilestone">{{canStudentEditProposal ? 'Sửa kế hoạch và gửi lại' : 'Sửa thông tin mốc'}}</button>
    </div>
    <div class="v07-meta">
      <span><strong>Deadline:</strong> {{readableDeadline(selected.deadlineAt)}}</span>
      <span><strong>Trạng thái:</strong> {{statusName(selected.status)}}</span>
    </div>
    <p><strong>Kết quả cần đạt:</strong> {{selected.expectedResult || 'Chưa cập nhật'}}</p>
    <p v-if="selected.proposedByStudent || selected.proposalStatus !== 'CONFIRMED'"><strong>Trạng thái kế hoạch:</strong> {{proposalStatusName(selected.proposalStatus)}}</p>
    <p v-if="selected.proposedByStudent && selected.proposalFeedback"><strong>Góp ý giảng viên:</strong> {{selected.proposalFeedback}}</p>
    <p v-if="role === 'Student' && selected.proposalStatus === 'PENDING_REVIEW'" class="v07-muted">Kế hoạch đang chờ giảng viên xem xét. Bạn có thể sửa ngày nộp ở phần chi tiết bên dưới mà không gửi thông báo lại; chưa thể cập nhật tiến độ hoặc nộp kết quả.</p>
    <p v-if="role === 'Student' && selected.proposalStatus === 'CHANGES_REQUESTED'" class="v07-muted">Nhấn “Sửa kế hoạch và gửi lại” để chỉnh theo góp ý, sau đó gửi cho giảng viên.</p>
    <!-- V096_DEADLINE_IN_DETAILS: edit selected milestone only, not in the catalogue table. -->
    <div v-if="canStudentEditDeadline(selected)" class="v096-deadline-section">
      <div v-if="deadlineEditingId === selected.id" class="v096-deadline-form">
        <label class="v095-deadline-editor">Ngày nộp mới
          <input v-model="deadlineDraft" type="date" :disabled="saving" required />
        </label>
        <p class="v07-muted">{{selected.proposalStatus === 'CONFIRMED' ? 'Đổi ngày sẽ yêu cầu giảng viên xác nhận lại và gửi thông báo.' : 'Kế hoạch chưa được xác nhận: đổi ngày không gửi thêm thông báo.'}}</p>
        <div class="v07-actions v095-deadline-actions">
          <button type="button" :disabled="saving" @click="saveDeadline(selected)">Lưu ngày</button>
          <button type="button" :disabled="saving" @click="deadlineEditingId = ''; deadlineDraft = ''">Hủy</button>
        </div>
      </div>
      <button v-else type="button" :disabled="saving" @click="beginDeadlineEdit(selected)">Sửa ngày</button>
    </div>
    <form v-if="canLecturerReviewProposal" class="v07-form" @submit.prevent>
      <h4>Xác nhận kế hoạch hoặc ngày nộp Milestone</h4>
      <p class="v07-muted">Nếu sinh viên đề xuất thay đổi deadline sau khi đã duyệt, hãy xác nhận lại ngày nộp mới. Bạn cũng có thể sửa thông tin trước khi xác nhận.</p>
      <label>Góp ý <textarea v-model.trim="proposalFeedback" rows="3" maxlength="2000" placeholder="Nhập nhận xét nếu yêu cầu sửa" /></label>
      <div class="v07-actions">
        <button type="button" :disabled="saving" @click="reviewProposal(true)">Xác nhận và gửi sinh viên thực hiện</button>
        <button type="button" :disabled="saving" @click="reviewProposal(false)">Yêu cầu sinh viên sửa kế hoạch</button>
      </div>
    </form>
  </article>

  <!-- Progress-only page. -->
  <article v-if="selected && isProgressPage" class="panel v07-block">
    <h3>Cập nhật tiến độ: {{selected.title}}</h3>
    <form v-if="canStudentUpdate" class="v07-form" @submit.prevent="submitProgress">
      <label>Hoàn thành (%) <input v-model.number="percent" type="number" min="0" max="100" required /></label>
      <label>Nội dung đã thực hiện <textarea v-model.trim="workDone" rows="3" maxlength="4000" required /></label>
      <label>Ghi chú <textarea v-model.trim="note" rows="2" maxlength="2000" /></label>
      <button :disabled="saving" type="submit">Lưu tiến độ</button>
    </form>
    <p v-else class="v07-muted">{{role !== 'Student' ? 'Chỉ sinh viên phụ trách Project được cập nhật tiến độ.' : selected.proposalStatus !== 'CONFIRMED' ? 'Kế hoạch chưa được giảng viên xác nhận. Hãy hoàn tất tại mục Milestone trước.' : selected.status === 'SUBMITTED' ? 'Đã gửi kết quả, đang chờ giảng viên đánh giá.' : selected.status === 'APPROVED' ? 'Milestone đã được phê duyệt.' : 'Hiện chưa thể cập nhật tiến độ Milestone này.'}}</p>
    <h4>Lịch sử cập nhật tiến độ ({{history?.progress.length ?? 0}})</h4>
    <div v-if="history" class="v07-history">
      <p v-for="h in history.progress" :key="h.id">{{readableDate(h.createdAt)}} · {{h.percentComplete}}% · {{h.workDone}}<span v-if="h.note"> · {{h.note}}</span></p>
      <p v-if="!history.progress.length" class="v07-muted">Chưa có cập nhật.</p>
    </div>
  </article>

  <!-- Submission-only page. -->
  <article v-if="selected && isSubmissionsPage" class="panel v07-block">
    <h3>Nộp kết quả: {{selected.title}}</h3>
    <p><strong>Kết quả cần đạt:</strong> {{selected.expectedResult || 'Chưa cập nhật'}}</p>
    <p v-if="history?.reviews.length" class="v07-notice">
      <strong>{{selected.status === 'REVISION_REQUIRED' ? 'Giảng viên yêu cầu sửa:' : 'Nhận xét gần nhất của giảng viên:'}}</strong>
      {{history.reviews[history.reviews.length - 1].feedback || (selected.status === 'REVISION_REQUIRED' ? 'Vui lòng chỉnh sửa và nộp lại.' : 'Không có nhận xét bổ sung.')}}
    </p>
    <p v-if="canStudentSubmit && selected.status === 'SUBMITTED'" class="v07-muted">Đã gửi kết quả và đang chờ giảng viên đánh giá. Bạn vẫn có thể chỉnh sửa, nộp lại bản mới; các bản trước được lưu trong lịch sử.</p>
    <form v-if="canStudentSubmit" class="v07-form" @submit.prevent="submitResult">
      <label>Tóm tắt kết quả <textarea v-model.trim="resultSummary" rows="3" maxlength="4000" required /></label>
      <label>Đường dẫn tài liệu / kết quả (nếu có) <input v-model.trim="documentReference" maxlength="1000" placeholder="URL hoặc tham chiếu tài liệu" /></label>
      <button :disabled="saving" type="submit">{{selected.status === 'SUBMITTED' ? 'Nộp lại kết quả' : 'Nộp cho giảng viên'}}</button>
    </form>
    <p v-else class="v07-muted">{{role !== 'Student' ? 'Chỉ sinh viên phụ trách Project được nộp kết quả.' : selected.proposalStatus !== 'CONFIRMED' ? 'Kế hoạch chưa được xác nhận. Hãy hoàn tất tại mục Milestone trước.' : selected.status === 'APPROVED' ? 'Milestone đã được phê duyệt.' : 'Hiện chưa thể nộp kết quả cho Milestone này.'}}</p>
    <h4>Lịch sử bản nộp ({{history?.submissions.length ?? 0}})</h4>
    <div v-if="history" class="v07-history">
      <p v-for="(h, index) in history.submissions" :key="h.id"><strong>{{index === history.submissions.length - 1 ? 'Bản mới nhất' : `Bản ${index + 1}`}}</strong> · {{readableDate(h.submittedAt)}} · {{h.resultSummary}}<span v-if="h.documentReference"> · {{h.documentReference}}</span></p>
      <p v-if="!history.submissions.length" class="v07-muted">Chưa có bản nộp.</p>
    </div>
  </article>

  <!-- Lecturer evaluation and review history only. -->
  <article v-if="selected && isEvaluationsPage" class="panel v07-block">
    <h3>Đánh giá kết quả: {{selected.title}}</h3>
    <p v-if="selected.proposalStatus !== 'CONFIRMED'" class="v07-muted">Kế hoạch Milestone cần được giảng viên xác nhận tại mục Milestone trước khi nộp và đánh giá kết quả.</p>
    <div v-if="history?.submissions.length" class="v07-last-submission">
      <h4>Bản nộp mới nhất</h4>
      <p><strong>Thời gian:</strong> {{readableDate(history.submissions[history.submissions.length - 1].submittedAt)}}</p>
      <p><strong>Tóm tắt:</strong> {{history.submissions[history.submissions.length - 1].resultSummary}}</p>
      <p v-if="history.submissions[history.submissions.length - 1].documentReference"><strong>Tài liệu:</strong> {{history.submissions[history.submissions.length - 1].documentReference}}</p>
    </div>
    <form v-if="canLecturerReview" class="v07-form" @submit.prevent>
      <h4>Đánh giá kết quả sinh viên đã nộp</h4>
      <label>Nhận xét <textarea v-model.trim="feedback" rows="3" maxlength="4000" placeholder="Nhập nhận xét khi yêu cầu sửa" /></label>
      <div class="v07-actions">
        <button type="button" :disabled="saving" @click="review(true)">Phê duyệt</button>
        <button type="button" :disabled="saving" @click="review(false)">Yêu cầu chỉnh sửa</button>
      </div>
    </form>
    <p v-else class="v07-muted">{{role === 'Lecturer' ? 'Chỉ có thể đánh giá khi sinh viên đã nộp kết quả và Project đang hoạt động.' : 'Bạn có thể xem nhận xét của giảng viên tại đây.'}}</p>
    <h4>Lịch sử nhận xét giảng viên ({{history?.reviews.length ?? 0}})</h4>
    <div v-if="history" class="v07-history">
      <p v-for="h in history.reviews" :key="h.id">{{readableDate(h.createdAt)}} · {{h.isApproved ? 'Phê duyệt' : 'Yêu cầu sửa'}} · {{h.feedback || 'Không có nhận xét'}}</p>
      <p v-if="!history.reviews.length" class="v07-muted">Chưa có nhận xét.</p>
    </div>
  </article>
</template>
<style scoped>
.v07-block { margin-bottom: 16px; padding: 18px; }
.v07-head { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; }
.v07-meta { display: flex; flex-wrap: wrap; gap: 12px 24px; margin-top: 12px; }
.v07-table th,.v07-table td { padding: 12px 14px; vertical-align: top; }
.v07-muted { color: var(--text-muted,#64748b); font-size: .9em; margin: 4px 0; }
.v07-late,.v07-error { color: #b42318; }
.v07-success { color: #16703d; }
.v07-notice { padding: 10px 14px; background: var(--surface,#f1f5f9); border-radius: 6px; margin-bottom: 12px; }
.v07-form { display: grid; gap: 12px; border-top: 1px solid #cbd5e1; margin-top: 18px; padding-top: 16px; }
.v07-form label,.v07-block > label { display: grid; gap: 5px; }
.v07-form :is(input,textarea,select),.v07-block > label select { width: 100%; box-sizing: border-box; padding: 9px 10px; border: 1px solid #94a3b8; border-radius: 5px; }
.v07-grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(190px,1fr)); gap: 12px; }
.v07-actions { display: flex; gap: 10px; flex-wrap: wrap; }
.v07-form button,.v07-head button,.v07-table button { width: fit-content; padding: 8px 13px; border-radius: 5px; cursor: pointer; }
.v07-history > div { border-top: 1px solid #cbd5e1; padding: 12px 0; overflow-wrap: anywhere; }
.v07-history p { padding: 8px 0; border-bottom: 1px solid #e2e8f0; overflow-wrap: anywhere; }
.v095-deadline-editor { display: grid; gap: 5px; min-width: 155px; }
.v095-deadline-editor input { padding: 6px 8px; border: 1px solid #94a3b8; border-radius: 5px; max-width: 190px; }
.v095-deadline-actions { margin-top: 6px; gap: 5px; }
.v096-deadline-section { margin-top: 14px; }
.v096-deadline-section button { width: fit-content; padding: 8px 13px; border-radius: 5px; cursor: pointer; }
.v096-deadline-form { display: grid; gap: 8px; }
.v07-last-submission { border: 1px solid #cbd5e1; border-radius: 6px; padding: 12px 14px; margin-top: 12px; overflow-wrap: anywhere; }
</style>
