<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { HttpError } from '../api/client'
import { periodsApi } from '../features/periods/periods.api'
import { topicsApi } from '../features/topics/topics.api'
import { supervisionApi } from '../features/supervision/supervision.api'
import type { RegistrationPeriod, Topic } from '../features/topics/topics.types'
import type { LecturerCapacityV06, LecturerRequestV06, ProjectV06, JointTopicDraft } from '../features/supervision/supervision.api'
import type { ModuleId } from '../app/modules'
const props = defineProps<{moduleId: ModuleId}>()
const auth = useAuthStore()
const role = computed(() => auth.role)
const periods = ref<RegistrationPeriod[]>([])
const periodId = ref('')
const capacities = ref<LecturerCapacityV06[]>([])
const requests = ref<LecturerRequestV06[]>([])
const projects = ref<ProjectV06[]>([])
const topics = ref<Topic[]>([])
const topicId = ref('')
const jointTopicId = ref('new')
const jointLecturerId = ref('')
const allTopics = ref<Topic[]>([])
const jointDraft = ref<JointTopicDraft>({ title: '', description: '', objective: '', expectedContent: '', proposedTechnology: '' })
const revisingId = ref('')
const revisingDraft = ref<JointTopicDraft>({ title: '', description: '', objective: '', expectedContent: '', proposedTechnology: '' })
const maxStudents = ref(1)
const loading = ref(false), error = ref(''), success = ref('')
const ownCapacity = computed(() => capacities.value.find(c => c.lecturerUserId === auth.currentUser?.userId))
const selectedPeriod = computed(() => periods.value.find(p => p.id === periodId.value))
const activePeriod = computed(() => selectedPeriod.value?.isOpen &&
  new Date(selectedPeriod.value.startsAt).getTime() <= Date.now() &&
  new Date(selectedPeriod.value.endsAt).getTime() > Date.now())
const publicTopics = computed(() => allTopics.value.filter(t => t.isRegistrationOpen &&
  (t.status === 'APPROVED' || t.status === 'PUBLISHED') && !t.reservedForStudentUserId))
const existingRequest = (lId: string) => requests.value.some(r => r.lecturerUserId === lId &&
  r.topicId === topicId.value && r.registrationPeriodId === periodId.value && r.status === 'PENDING')
function message(e: unknown) {
  return e instanceof HttpError ? (e.payload?.message || `API ${e.status}`)
    : e instanceof Error ? e.message : 'Không thể tải dữ liệu.'
}
async function load() {
  loading.value = true
  error.value = ''
  try {
    periods.value = await periodsApi.list()
    if (!periodId.value || !periods.value.some(p => p.id === periodId.value)) {
      const current = periods.value.find(p => p.isOpen && new Date(p.startsAt).getTime() <= Date.now() &&
        new Date(p.endsAt).getTime() > Date.now())
      periodId.value = (current || periods.value[0])?.id || ''
    }
    if (props.moduleId === 'capacity') {
      capacities.value = periodId.value ? await supervisionApi.capacities(periodId.value) : []
      maxStudents.value = ownCapacity.value?.maxStudents ?? 1
    }
    if (props.moduleId === 'lecturerrequests') {
      requests.value = await supervisionApi.requests()
      if (role.value === 'Student') {
        topics.value = await topicsApi.mine()
        if (!topics.value.some(t => t.id === topicId.value)) topicId.value = topics.value[0]?.id || ''
        allTopics.value = (await topicsApi.list('', 1, 100)).items
        capacities.value = periodId.value ? await supervisionApi.capacities(periodId.value) : []
      }
    }
    if (props.moduleId === 'projects') projects.value = await supervisionApi.projects()
  } catch (e) { error.value = message(e) }
  finally { loading.value = false }
}
async function saveCapacity() {
  error.value = ''; success.value = ''
  if (!periodId.value) { error.value = 'Chọn đợt đăng ký.'; return }
  if (!Number.isInteger(maxStudents.value) || maxStudents.value < 0 || maxStudents.value > 1000) {
    error.value = 'Giới hạn phải là số nguyên từ 0 đến 1000.'; return
  }
  loading.value = true
  try {
    await supervisionApi.updateCapacity(periodId.value, maxStudents.value)
    await load(); success.value = 'Đã lưu giới hạn hướng dẫn.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function submit(lecturer: LecturerCapacityV06) {
  if (!topicId.value || !periodId.value) { error.value = 'Chọn đề tài đã được xác nhận.'; return }
  if (!window.confirm(`Gửi yêu cầu hướng dẫn đến ${lecturer.lecturerName}?`)) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.submit(topicId.value, lecturer.lecturerUserId, periodId.value)
    await load(); success.value = 'Đã gửi yêu cầu đến giảng viên.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
function startRevision(item: LecturerRequestV06) {
  revisingId.value = item.id
  revisingDraft.value = {
    title: item.draftTitle || item.topicTitle,
    description: item.draftDescription || '',
    objective: item.draftObjective || '',
    expectedContent: item.draftExpectedContent || '',
    proposedTechnology: item.draftProposedTechnology || '',
  }
  error.value = ''; success.value = ''
}
async function submitJoint() {
  if (!activePeriod.value || !periodId.value || !jointLecturerId.value) {
    error.value = 'Chọn đợt còn hiệu lực và giảng viên.'; return
  }
  const chosen = capacities.value.find(c => c.lecturerUserId === jointLecturerId.value)
  if (!chosen || chosen.remaining <= 0) { error.value = 'Giảng viên đã đủ số lượng hướng dẫn.'; return }
  if (jointTopicId.value === 'new' && Object.values(jointDraft.value).some(s => !s.trim())) {
    error.value = 'Nhập đầy đủ năm trường của đề tài tự đề xuất.'; return
  }
  if (jointTopicId.value !== 'new' && !publicTopics.value.some(t => t.id === jointTopicId.value)) {
    error.value = 'Chọn đề tài đang mở đăng ký.'; return
  }
  if (!window.confirm('Gửi đề tài và yêu cầu hướng dẫn đến giảng viên đã chọn?')) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.combined({ topicId: jointTopicId.value === 'new' ? null : jointTopicId.value,
      lecturerUserId: jointLecturerId.value, registrationPeriodId: periodId.value,
      draft: jointTopicId.value === 'new' ? jointDraft.value : null })
    await load(); success.value = 'Đã gửi đề tài và giảng viên để xét duyệt.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function resubmitJoint() {
  if (Object.values(revisingDraft.value).some(s => !s.trim())) {
    error.value = 'Điền đầy đủ nội dung đề tài trước khi gửi lại.'; return
  }
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.resubmit(revisingId.value, revisingDraft.value)
    revisingId.value = ''; await load(); success.value = 'Đã gửi lại đề tài cho đúng giảng viên.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function requestRevision(item: LecturerRequestV06) {
  const reason = window.prompt('Yêu cầu sinh viên chỉnh sửa nội dung gì?')
  if (reason === null) return
  if (!reason.trim()) { error.value = 'Cần nêu nội dung yêu cầu sửa.'; return }
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.revision(item.id, reason.trim())
    await load(); success.value = 'Đã gửi yêu cầu chỉnh sửa cho sinh viên.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function decide(item: LecturerRequestV06, accepted: boolean) {
  let reason = ''
  if (!accepted) {
    const entry = window.prompt('Nhập lý do từ chối:')
    if (entry === null) return
    reason = entry.trim()
    if (!reason) { error.value = 'Vui lòng nhập lý do.'; return }
  } else if (!window.confirm(`Chấp nhận hướng dẫn ${item.studentName} với đề tài “${item.isCombined ? (item.draftTitle || item.topicTitle) : item.topicTitle}”? Hệ thống sẽ tạo Project và trừ một suất hướng dẫn.`)) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    if (accepted) await supervisionApi.accept(item.id)
    else await supervisionApi.reject(item.id, reason)
    await load(); success.value = accepted ? 'Đã nhận sinh viên và tạo Project.' : 'Đã từ chối yêu cầu.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
watch(() => props.moduleId, () => void load())
watch(periodId, () => { if (props.moduleId === 'capacity' || props.moduleId === 'lecturerrequests') void load() })
onMounted(() => void load())
</script>
<template>
  <section class="intro"><span class="section-chip">Hướng dẫn · v0.6</span>
    <h2>{{moduleId === 'capacity' ? 'Sức chứa giảng viên' : moduleId === 'projects' ? 'Dự án của tôi' : 'Đăng ký đề tài & giảng viên hướng dẫn'}}</h2>
    <p>{{moduleId === 'projects' ? 'Project được tự động tạo sau khi giảng viên chấp nhận hướng dẫn.' : 'Dữ liệu lấy từ API và SQL Server, được kiểm tra quyền tại Backend.'}}</p>
  </section>
  <p v-if="error" class="user-alert error" role="alert">{{error}}</p>
  <p v-if="success" class="user-alert success" role="status">{{success}}</p>
  <article v-if="moduleId !== 'projects'" class="panel users-panel">
    <div class="panel-heading"><h3>Đợt đăng ký</h3></div>
    <label>Chọn đợt
      <select v-model="periodId"><option value="" disabled>Chọn đợt</option>
        <option v-for="p in periods" :key="p.id" :value="p.id">{{p.name}} · {{p.isOpen ? 'Mở' : 'Đóng'}}</option>
      </select>
    </label>
    <p v-if="!periods.length" class="secondary-cell">Chưa có đợt đăng ký. Admin cần tạo đợt ở mục Đợt đăng ký.</p>
  </article>
  <article v-if="moduleId === 'capacity' && role === 'Lecturer' && periodId" class="panel user-editor">
    <h3>Thiết lập số sinh viên tối đa của bạn</h3>
    <form class="user-form" @submit.prevent="saveCapacity">
      <label>Giới hạn sinh viên<input v-model.number="maxStudents" type="number" min="0" max="1000" step="1" required /></label>
      <p>Đã nhận: {{ownCapacity?.currentStudents ?? 0}}. Không thể đặt giới hạn thấp hơn số sinh viên đã nhận.</p>
      <button class="action-primary" type="submit" :disabled="loading">Lưu sức chứa</button>
    </form>
  </article>
  <article v-if="moduleId === 'lecturerrequests' && role === 'Student'" class="panel user-editor">
    <h3>Đăng ký đề tài và chọn giảng viên cùng lúc</h3>
    <p>Chọn đề tài đang mở hoặc tự đề xuất đề tài mới. Giảng viên duyệt cả đề tài và yêu cầu hướng dẫn trong một lần.</p>
    <form class="user-form" @submit.prevent="submitJoint">
      <label>Đề tài
        <select v-model="jointTopicId">
          <option value="new">Tự đề xuất đề tài mới</option>
          <option v-for="t in publicTopics" :key="t.id" :value="t.id">{{t.title}}</option>
        </select>
      </label>
      <template v-if="jointTopicId === 'new'">
        <label>Tên đề tài<input v-model="jointDraft.title" required maxlength="300" /></label>
        <label>Mô tả<textarea v-model="jointDraft.description" required maxlength="4000" /></label>
        <label>Mục tiêu<textarea v-model="jointDraft.objective" required maxlength="2000" /></label>
        <label>Nội dung dự kiến<textarea v-model="jointDraft.expectedContent" required maxlength="2000" /></label>
        <label>Công nghệ dự kiến<textarea v-model="jointDraft.proposedTechnology" required maxlength="1000" /></label>
      </template>
      <label>Giảng viên
        <select v-model="jointLecturerId" required>
          <option value="" disabled>Chọn giảng viên</option>
          <option v-for="c in capacities.filter(c => c.remaining > 0)" :key="c.lecturerUserId" :value="c.lecturerUserId">{{c.lecturerName}} · Còn {{c.remaining}} chỗ</option>
        </select>
      </label>
      <button class="action-primary" type="submit" :disabled="loading || !activePeriod">Gửi đăng ký đề tài + giảng viên</button>
    </form>
  </article>
  <article v-if="moduleId === 'capacity' || (moduleId === 'lecturerrequests' && role === 'Student')" class="panel users-panel">
    <h3>Danh sách giảng viên và vị trí còn trống</h3>
    <label v-if="moduleId === 'lecturerrequests' && role === 'Student'">Đề tài đã nhận
      <select v-model="topicId"><option value="" disabled>Chọn đề tài của bạn</option>
        <option v-for="t in topics" :key="t.id" :value="t.id">{{t.title}}</option>
      </select>
    </label>
    <p v-if="moduleId === 'lecturerrequests' && role === 'Student' && !topics.length" class="secondary-cell">Bạn cần được xác nhận sở hữu một đề tài trước khi đăng ký giảng viên.</p>
    <div class="table-scroller"><table><thead><tr><th>Giảng viên</th><th>Chuyên môn</th><th>Đã nhận / Tối đa</th><th>Còn trống</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="!capacities.length"><td colspan="5" class="empty-cell">Chưa có giảng viên hoặc chưa chọn đợt.</td></tr>
      <tr v-for="c in capacities" :key="c.lecturerUserId"><td>{{c.lecturerName}}</td><td>{{c.specialty || 'Chưa cập nhật'}}</td>
        <td>{{c.currentStudents}} / {{c.maxStudents}}</td><td>{{c.remaining}} · {{c.status}}</td>
        <td><button v-if="moduleId === 'lecturerrequests' && role === 'Student'" type="button" class="action-secondary"
          :disabled="loading || !activePeriod || !topicId || c.remaining <= 0 || existingRequest(c.lecturerUserId)"
          @click="submit(c)">{{existingRequest(c.lecturerUserId) ? 'Đã gửi' : 'Đăng ký hướng dẫn'}}</button></td></tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId === 'lecturerrequests'" class="panel users-panel">
    <h3>{{role === 'Lecturer' ? 'Yêu cầu gửi đến bạn' : 'Yêu cầu đăng ký giảng viên'}}</h3>
    <div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Sinh viên</th><th>Giảng viên</th><th>Đợt</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="!requests.length"><td colspan="6" class="empty-cell">Chưa có yêu cầu đăng ký hướng dẫn.</td></tr>
      <tr v-for="r in requests" :key="r.id"><td>{{r.isCombined ? (r.draftTitle || r.topicTitle) : r.topicTitle}}
          <details v-if="r.isCombined"><summary>Xem nội dung đề tài</summary>
            <p><strong>Mô tả:</strong> {{r.draftDescription}}</p>
            <p><strong>Mục tiêu:</strong> {{r.draftObjective}}</p>
            <p><strong>Nội dung:</strong> {{r.draftExpectedContent}}</p>
            <p><strong>Công nghệ:</strong> {{r.draftProposedTechnology}}</p>
          </details></td><td>{{r.studentName}}</td><td>{{r.lecturerName}}</td><td>{{r.periodName}}</td>
        <td>{{r.status === 'REVISION_REQUIRED' ? 'CẦN CHỈNH SỬA' : r.status}}<div v-if="r.rejectionReason" class="secondary-cell">Phản hồi: {{r.rejectionReason}}</div>
          <div v-if="r.isCombined" class="secondary-cell">Đăng ký kết hợp</div></td>
        <td class="actions"><template v-if="role === 'Lecturer' && r.status === 'PENDING'">
          <button type="button" :disabled="loading" @click="decide(r, true)">Chấp nhận & tạo Project</button>
          <button v-if="r.isCombined" type="button" :disabled="loading" @click="requestRevision(r)">Yêu cầu sửa</button>
          <button type="button" :disabled="loading" @click="decide(r, false)">Từ chối</button>
        </template>
        <button v-if="role === 'Student' && r.status === 'REVISION_REQUIRED' && r.isCombined"
          type="button" :disabled="loading" @click="startRevision(r)">Sửa và gửi lại</button></td></tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId === 'lecturerrequests' && role === 'Student' && revisingId" class="panel user-editor">
    <h3>Chỉnh sửa đề tài theo góp ý giảng viên</h3>
    <form class="user-form" @submit.prevent="resubmitJoint">
      <label>Tên đề tài<input v-model="revisingDraft.title" required maxlength="300" /></label>
      <label>Mô tả<textarea v-model="revisingDraft.description" required maxlength="4000" /></label>
      <label>Mục tiêu<textarea v-model="revisingDraft.objective" required maxlength="2000" /></label>
      <label>Nội dung dự kiến<textarea v-model="revisingDraft.expectedContent" required maxlength="2000" /></label>
      <label>Công nghệ dự kiến<textarea v-model="revisingDraft.proposedTechnology" required maxlength="1000" /></label>
      <button class="action-primary" type="submit" :disabled="loading">Gửi lại giảng viên</button>
      <button class="action-secondary" type="button" @click="revisingId = ''">Hủy chỉnh sửa</button>
    </form>
  </article>
  <article v-if="moduleId === 'projects'" class="panel users-panel"><h3>Dự án đã được xác nhận</h3>
    <div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Sinh viên</th><th>Giảng viên hướng dẫn</th><th>Đợt</th><th>Trạng thái</th></tr></thead><tbody>
      <tr v-if="!projects.length"><td colspan="5" class="empty-cell">Chưa có Project. Cần hoàn tất đăng ký đề tài và xác nhận giảng viên.</td></tr>
      <tr v-for="p in projects" :key="p.id"><td>{{p.topicTitle}}</td><td>{{p.studentName}}</td><td>{{p.lecturerName}}</td><td>{{p.periodName}}</td><td>{{p.status}}</td></tr>
    </tbody></table></div>
  </article>
</template>
