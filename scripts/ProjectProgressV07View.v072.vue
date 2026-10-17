<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { HttpError } from '../api/client'
import { projectProgressApi, type ProjectV07, type MilestoneV07, type MilestoneForm, type ProgressHistory } from '../features/progress/projectProgress.api'

const props = defineProps<{ moduleId: string }>()
const route = useRoute()
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
const emptyMilestone = (): MilestoneForm => ({title:'', description:'', startAt:'', deadlineAt:'', expectedResult:''})
const milestoneForm = ref<MilestoneForm>(emptyMilestone())
const percent = ref(0)
const workDone = ref('')
const note = ref('')
const resultSummary = ref('')
const documentReference = ref('')
const feedback = ref('')
const selectedProject = computed(() => projects.value.find(x => x.id === projectId.value))
const selected = computed(() => milestones.value.find(x => x.id === selectedId.value))
const canWork = computed(() => selectedProject.value && ['REGISTERED','IN_PROGRESS','DRAFT'].includes(selectedProject.value.status))
const canStudentUpdate = computed(() => role.value === 'Student' && canWork.value && selected.value && selected.value.proposalStatus === 'CONFIRMED' && !['SUBMITTED','APPROVED'].includes(selected.value.status))
const canStudentEditProposal = computed(() => role.value === 'Student' && canWork.value && selected.value?.proposedByStudent && selected.value.proposalStatus === 'CHANGES_REQUESTED')
const canLecturerReviewProposal = computed(() => role.value === 'Lecturer' && canWork.value && selected.value?.proposedByStudent && selected.value.proposalStatus === 'PENDING_REVIEW')
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
const dateLocal = (date: string) => {
  const d = new Date(date)
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 16)
}
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
  error.value = ''; success.value = ''; showMilestoneForm.value = false
  if (!projectId.value) { milestones.value = []; selectedId.value = ''; history.value = null; return }
  try {
    milestones.value = await projectProgressApi.milestones(projectId.value)
    if (!milestones.value.some(x => x.id === selectedId.value)) selectedId.value = milestones.value[0]?.id || ''
    await loadHistory()
  } catch(e) { error.value = errorText(e) }
}
async function loadHistory() {
  history.value = null
  if (!selectedId.value) return
  try { history.value = await projectProgressApi.history(selectedId.value) }
  catch(e) { error.value = errorText(e) }
  percent.value = selected.value?.percentComplete ?? 0
  workDone.value = ''; note.value = ''; resultSummary.value = ''; documentReference.value = ''; feedback.value = ''; proposalFeedback.value = ''
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
function newMilestone() {
  editing.value = false; milestoneForm.value = emptyMilestone(); showMilestoneForm.value = true
}
function editMilestone() {
  if (!selected.value) return
  editing.value = true
  milestoneForm.value = {title:selected.value.title,description:selected.value.description || '',expectedResult:selected.value.expectedResult || '',
    startAt:dateLocal(selected.value.startAt),deadlineAt:dateLocal(selected.value.deadlineAt)}
  showMilestoneForm.value = true
}
async function saveMilestone() {
  if (!projectId.value || !milestoneForm.value.startAt || !milestoneForm.value.deadlineAt) return
  const request: MilestoneForm = {...milestoneForm.value,
    startAt:new Date(milestoneForm.value.startAt).toISOString(),
    deadlineAt:new Date(milestoneForm.value.deadlineAt).toISOString()}
  if (new Date(request.deadlineAt).getTime() < new Date(request.startAt).getTime()) { error.value = 'Deadline không thể trước ngày bắt đầu.'; return }
  const wasEditing = editing.value
  const id = selectedId.value
  await run(() => wasEditing ? projectProgressApi.update(id,request) : projectProgressApi.create(projectId.value,request),
    wasEditing
      ? (role.value === 'Student' ? 'Đã sửa và gửi lại kế hoạch cho giảng viên.' : 'Đã cập nhật Milestone.')
      : (role.value === 'Student' ? 'Đã gửi đề xuất Milestone cho giảng viên.' : 'Đã tạo Milestone.'))
}
function submitProgress() {
  if (!selected.value) return
  void run(() => projectProgressApi.progress(selectedId.value, percent.value, workDone.value, note.value), 'Đã cập nhật tiến độ.')
}
function submitResult() {
  if (!selected.value) return
  void run(() => projectProgressApi.submit(selectedId.value,resultSummary.value,documentReference.value), 'Đã nộp kết quả cho giảng viên.')
}
function review(isApproved: boolean) {
  if (!selected.value) return
  if (!isApproved && !feedback.value.trim()) { error.value = 'Cần nhập nhận xét khi yêu cầu sinh viên sửa.'; return }
  void run(() => projectProgressApi.review(selectedId.value,isApproved,feedback.value), isApproved ? 'Đã phê duyệt Milestone.' : 'Đã gửi yêu cầu chỉnh sửa.')
}
watch(projectId, () => { void loadMilestones() })
watch(selectedId, () => { showMilestoneForm.value = false; void loadHistory() })
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
    <h3>Chọn Project</h3>
    <p v-if="loading">Đang tải dữ liệu…</p>
    <label v-else>Project của {{role === 'Student' ? 'bạn' : role === 'Lecturer' ? 'sinh viên bạn hướng dẫn' : 'hệ thống'}}
      <select v-model="projectId" :disabled="saving">
        <option v-if="!projects.length" value="">Chưa có Project</option>
        <option v-for="p in projects" :key="p.id" :value="p.id">{{p.topicTitle}} · {{p.studentName}} · {{p.lecturerName}}</option>
      </select>
    </label>
    <div v-if="selectedProject" class="v07-meta">
      <span>Trạng thái: <strong>{{statusName(selectedProject.status)}}</strong></span>
      <span>Đã duyệt: <strong>{{selectedProject.approvedCount}}/{{selectedProject.milestoneCount}}</strong></span>
      <span>Tiến độ tổng hợp: <strong>{{selectedProject.percentComplete}}%</strong></span>
    </div>
  </article>

  <!-- Milestone catalogue and plan approvals, without progress/submission forms. -->
  <article v-if="selectedProject && isMilestonesPage" class="panel v07-block">
    <div class="v07-head">
      <h3>Milestones của Project</h3>
      <button v-if="['Lecturer','Student'].includes(role || '') && canWork" type="button" @click="newMilestone">{{role === 'Student' ? '+ Đề xuất Milestone' : '+ Tạo Milestone'}}</button>
    </div>
    <div class="table-scroller"><table class="v07-table">
      <thead><tr><th>Milestone</th><th>Deadline</th><th>Tiến độ</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
      <tbody>
        <tr v-for="m in milestones" :key="m.id">
          <td><strong>{{m.title}}</strong><p class="v07-muted">{{m.description}}</p></td>
          <td>{{readableDate(m.deadlineAt)}}</td><td>{{m.percentComplete}}%</td>
          <td><span v-if="m.proposalStatus !== 'CONFIRMED'">{{proposalStatusName(m.proposalStatus)}}</span><span v-else>{{statusName(m.status)}}</span><strong v-if="m.isOverdue" class="v07-late"> · Quá hạn</strong></td>
          <td><button type="button" :disabled="saving" @click="selectedId = m.id">{{selectedId === m.id ? 'Đang xem' : 'Chi tiết'}}</button></td>
        </tr>
        <tr v-if="!milestones.length"><td colspan="5" class="empty-cell">Chưa có Milestone. Giảng viên có thể tạo mốc hoặc sinh viên đề xuất kế hoạch mới.</td></tr>
      </tbody>
    </table></div>
    <form v-if="['Lecturer','Student'].includes(role || '') && showMilestoneForm" class="v07-form" @submit.prevent="saveMilestone">
      <h4>{{editing ? (role === 'Student' ? 'Sửa kế hoạch theo góp ý giảng viên' : 'Chỉnh sửa Milestone') : (role === 'Student' ? 'Đề xuất Milestone gửi giảng viên' : 'Tạo Milestone')}}</h4>
      <label>Tên Milestone <input v-model.trim="milestoneForm.title" required maxlength="300" /></label>
      <label>Mô tả <textarea v-model.trim="milestoneForm.description" maxlength="4000" rows="2" /></label>
      <div class="v07-grid"><label>Bắt đầu <input v-model="milestoneForm.startAt" type="datetime-local" lang="en-GB" required /></label>
        <label>Deadline <input v-model="milestoneForm.deadlineAt" type="datetime-local" lang="en-GB" required /></label></div>
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
      <span>Deadline: <strong>{{readableDate(selected.deadlineAt)}}</strong></span>
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
      <span><strong>Bắt đầu:</strong> {{readableDate(selected.startAt)}}</span>
      <span><strong>Hạn:</strong> {{readableDate(selected.deadlineAt)}}</span>
      <span><strong>Trạng thái:</strong> {{statusName(selected.status)}}</span>
    </div>
    <p><strong>Kết quả cần đạt:</strong> {{selected.expectedResult || 'Chưa cập nhật'}}</p>
    <p v-if="selected.proposedByStudent"><strong>Đề xuất của sinh viên:</strong> {{proposalStatusName(selected.proposalStatus)}}</p>
    <p v-if="selected.proposedByStudent && selected.proposalFeedback"><strong>Góp ý giảng viên:</strong> {{selected.proposalFeedback}}</p>
    <p v-if="role === 'Student' && selected.proposalStatus === 'PENDING_REVIEW'" class="v07-muted">Kế hoạch đang chờ giảng viên xem xét. Bạn chưa thể cập nhật tiến độ hoặc nộp kết quả.</p>
    <p v-if="role === 'Student' && selected.proposalStatus === 'CHANGES_REQUESTED'" class="v07-muted">Nhấn “Sửa kế hoạch và gửi lại” để chỉnh theo góp ý, sau đó gửi cho giảng viên.</p>
    <form v-if="canLecturerReviewProposal" class="v07-form" @submit.prevent>
      <h4>Xem xét kế hoạch Milestone sinh viên đề xuất</h4>
      <p class="v07-muted">Có thể bấm “Sửa thông tin mốc” để hiệu chỉnh trước khi xác nhận kế hoạch và gửi cho sinh viên thực hiện.</p>
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
    <form v-if="canStudentUpdate" class="v07-form" @submit.prevent="submitResult">
      <label>Tóm tắt kết quả <textarea v-model.trim="resultSummary" rows="3" maxlength="4000" required /></label>
      <label>Đường dẫn tài liệu / kết quả (nếu có) <input v-model.trim="documentReference" maxlength="1000" placeholder="URL hoặc tham chiếu tài liệu" /></label>
      <button :disabled="saving" type="submit">Nộp cho giảng viên</button>
    </form>
    <p v-else class="v07-muted">{{role !== 'Student' ? 'Chỉ sinh viên phụ trách Project được nộp kết quả.' : selected.proposalStatus !== 'CONFIRMED' ? 'Kế hoạch chưa được xác nhận. Hãy hoàn tất tại mục Milestone trước.' : selected.status === 'SUBMITTED' ? 'Đã nộp. Chờ giảng viên nhận xét hoặc phê duyệt.' : selected.status === 'APPROVED' ? 'Milestone đã được phê duyệt.' : 'Hiện chưa thể nộp kết quả cho Milestone này.'}}</p>
    <h4>Lịch sử bản nộp ({{history?.submissions.length ?? 0}})</h4>
    <div v-if="history" class="v07-history">
      <p v-for="h in history.submissions" :key="h.id">{{readableDate(h.submittedAt)}} · {{h.resultSummary}}<span v-if="h.documentReference"> · {{h.documentReference}}</span></p>
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
.v07-last-submission { border: 1px solid #cbd5e1; border-radius: 6px; padding: 12px 14px; margin-top: 12px; overflow-wrap: anywhere; }
</style>
