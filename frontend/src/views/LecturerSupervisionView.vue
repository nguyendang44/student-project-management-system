<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
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
const route = useRoute()
const router = useRouter()
const role = computed(() => auth.role)
const periods = ref<RegistrationPeriod[]>([])
const periodId = ref('')
const capacities = ref<LecturerCapacityV06[]>([])
const requests = ref<LecturerRequestV06[]>([])
const projects = ref<ProjectV06[]>([])
const topics = ref<Topic[]>([])
const jointTopicId = ref('new')
const jointLecturerId = ref('')
const allTopics = ref<Topic[]>([])
const blankDraft = (): JointTopicDraft => ({ title: '', description: '', objective: '', expectedContent: '', proposedTechnology: '' })
const jointDraft = ref<JointTopicDraft>(blankDraft())
const maxStudents = ref(1)
const loading = ref(false), error = ref(''), success = ref('')
const ownCapacity = computed(() => capacities.value.find(c => c.lecturerUserId === auth.currentUser?.userId))
const selectedPeriod = computed(() => periods.value.find(p => p.id === periodId.value))
const activePeriod = computed(() => selectedPeriod.value?.isOpen &&
  new Date(selectedPeriod.value.startsAt).getTime() <= Date.now() &&
  new Date(selectedPeriod.value.endsAt).getTime() > Date.now())
const publicTopics = computed(() => allTopics.value.filter(t => t.isRegistrationOpen &&
  (t.status === 'APPROVED' || t.status === 'PUBLISHED') && !t.reservedForStudentUserId))
const originalTopic = computed(() => publicTopics.value.find(t => t.id === jointTopicId.value))
const ownPendingProposals = computed(() => {
  const ids = new Set<string>()
  return requests.value.filter(r => r.isCombined && ['PENDING', 'OFFERED', 'REVISION_REQUIRED'].includes(r.status) &&
    !r.topicId.startsWith('00000000')).filter(r => {
      if (ids.has(r.topicId)) return false
      ids.add(r.topicId)
      return true
    }).filter(r => !publicTopics.value.some(t => t.id === r.topicId) &&
      !topics.value.some(t => t.id === r.topicId))
})
// Copy the public topic into an application draft. Never mutate the source Topic object.
function selectTopicDraft(id: string) {
  if (id === 'new') { jointDraft.value = blankDraft(); return }
  const source = publicTopics.value.find(t => t.id === id)
  if (source) {
    jointDraft.value = {
      title: source.title, description: source.description,
      objective: source.objective ?? 'Chưa xác định',
      expectedContent: source.expectedContent ?? 'Chưa xác định',
      proposedTechnology: source.proposedTechnology ?? 'Chưa xác định'
    }
    return
  }
  const pending = ownPendingProposals.value.find(r => r.topicId === id)
  jointDraft.value = pending ? {
    title: pending.draftTitle || pending.topicTitle,
    description: pending.draftDescription || '',
    objective: pending.draftObjective || '',
    expectedContent: pending.draftExpectedContent || '',
    proposedTechnology: pending.draftProposedTechnology || ''
  } : blankDraft()
}
watch(jointTopicId, id => selectTopicDraft(id))
async function applyTopicFromLink() {
  if (props.moduleId !== 'lecturerrequests' || role.value !== 'Student') return
  const id = route.query.topicId
  if (typeof id !== 'string' || !id) return
  if (!publicTopics.value.some(t => t.id === id)) {
    // The catalogue is paged (100 rows); fetch a selected topic directly if it is off-page.
    try {
      const chosen = await topicsApi.get(id)
      if (chosen.isRegistrationOpen && !chosen.reservedForStudentUserId &&
          ['APPROVED', 'PUBLISHED'].includes(chosen.status)) {
        allTopics.value = [...allTopics.value.filter(t => t.id !== id), chosen]
      } else {
        error.value = 'Đề tài được chọn đã đóng đăng ký hoặc đã có sinh viên.'
        return
      }
    } catch (e) { error.value = message(e); return }
  }
  jointTopicId.value = id
}
watch(() => route.query.topicId, () => { void applyTopicFromLink() })
// A lecturer selected in the capacity table is only preselected here.
// Do not change the topic, application draft, or submit a request automatically.
function selectLecturerFromLink() {
  if (props.moduleId !== 'lecturerrequests' || role.value !== 'Student') return
  const lecturerId = route.query.lecturerId
  if (typeof lecturerId !== 'string' || !lecturerId) return
  const selected = capacities.value.find(c => c.lecturerUserId === lecturerId)
  if (selected && selected.remaining > 0) {
    jointLecturerId.value = lecturerId
  } else {
    error.value = 'Giảng viên được chọn hiện không còn suất hướng dẫn hoặc không khả dụng.'
  }
}
watch(() => route.query.lecturerId, () => selectLecturerFromLink())
function startLecturerRegistration(c: LecturerCapacityV06) {
  if (role.value !== 'Student' || c.remaining <= 0) return
  void router.push({ path: '/lecturer-requests', query: { lecturerId: c.lecturerUserId } })
}
const offeredRequests = computed(() => requests.value.filter(r => r.status === 'OFFERED'))
function message(e: unknown) {
  return e instanceof HttpError ? (e.payload?.message || `API ${e.status}`)
    : e instanceof Error ? e.message : 'Không thể tải dữ liệu.'
}
async function load() {
  loading.value = true
  error.value = ''
  try {
    // Confirmation is a separate student-only page. Offers do not depend on the selected period.
    if (props.moduleId === 'lecturerconfirmation') {
      requests.value = await supervisionApi.requests()
      return
    }
    periods.value = await periodsApi.list()
    const currentPeriod = periods.value.find(p => p.isOpen &&
      new Date(p.startsAt).getTime() <= Date.now() && new Date(p.endsAt).getTime() > Date.now())
    if (role.value === 'Student') {
      // Student never selects a period manually. Use an active period for submissions;
      // if none is active, retain a period only to show the global capacity table.
      periodId.value = (currentPeriod || periods.value[0])?.id || ''
    } else if (!periodId.value || !periods.value.some(p => p.id === periodId.value)) {
      periodId.value = (currentPeriod || periods.value[0])?.id || ''
    }
    if (props.moduleId === 'capacity') {
      capacities.value = periodId.value ? await supervisionApi.capacities(periodId.value) : []
      maxStudents.value = ownCapacity.value?.maxStudents ?? 1
    }
    if (props.moduleId === 'lecturerrequests') {
      requests.value = await supervisionApi.requests()
      if (role.value === 'Student') {
        topics.value = await topicsApi.mine()
        allTopics.value = (await topicsApi.list('', 1, 100)).items
        await applyTopicFromLink()
        capacities.value = periodId.value ? await supervisionApi.capacities(periodId.value) : []
        selectLecturerFromLink()
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
function startRevision(item: LecturerRequestV06) {
  if (role.value !== 'Student' || !item.isCombined || item.status !== 'REVISION_REQUIRED') return
  // Keep the exact request ID: two lecturers can request different edits to one topic.
  // The dedicated Topic Registration page owns the edit form and resubmit action.
  void router.push({ path: '/topic-registrations', query: { revisionId: item.id } })
}
async function submitJoint() {
  if (!activePeriod.value || !periodId.value || !jointLecturerId.value) {
    error.value = !activePeriod.value ? 'Chưa có đợt đăng ký đang mở. Vui lòng chờ Admin mở đợt.' : 'Vui lòng chọn giảng viên hướng dẫn.'; return
  }
  const chosen = capacities.value.find(c => c.lecturerUserId === jointLecturerId.value)
  if (!chosen || chosen.remaining <= 0) { error.value = 'Giảng viên đã đủ số lượng hướng dẫn.'; return }
  if (Object.values(jointDraft.value).some(s => !s.trim())) {
    error.value = 'Nhập đầy đủ năm trường nội dung đề tài.'; return
  }
  if (jointTopicId.value !== 'new' && !publicTopics.value.some(t => t.id === jointTopicId.value) &&
      !ownPendingProposals.value.some(r => r.topicId === jointTopicId.value)) {
    error.value = 'Chọn đề tài đang mở đăng ký.'; return
  }
  if (!window.confirm('Gửi đề tài và yêu cầu hướng dẫn đến giảng viên đã chọn?')) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.combined({ topicId: jointTopicId.value === 'new' ? null : jointTopicId.value,
      lecturerUserId: jointLecturerId.value, registrationPeriodId: periodId.value,
      // Backend stores this draft on the lecturer request and never edits the public topic.
      draft: { ...jointDraft.value } })
    await load(); success.value = 'Đã gửi yêu cầu. Khi giảng viên đồng ý, bạn sẽ được chọn giảng viên cuối cùng.'
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
async function cancelJoint(item: LecturerRequestV06) {
  if (!window.confirm(item.isCombined
    ? `Hủy yêu cầu “${item.draftTitle || item.topicTitle}”? Nếu là đề tài do bạn tự tạo, đề tài sẽ bị xóa vĩnh viễn, không chuyển sang danh sách dự bị.`
    : `Hủy yêu cầu hướng dẫn cho “${item.topicTitle}”?`)) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    const result = await supervisionApi.cancel(item.id)
    await load()
    success.value = result.deletedTopic ? 'Đã xóa đề tài tự tạo và hủy yêu cầu.' : 'Đã hủy yêu cầu hướng dẫn.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function decide(item: LecturerRequestV06, accepted: boolean) {
  let reason = ''
  if (!accepted) {
    const entry = window.prompt('Nhập lý do từ chối:')
    if (entry === null) return
    reason = entry.trim()
    if (!reason) { error.value = 'Vui lòng nhập lý do.'; return }
  } else if (!window.confirm(`Đồng ý hướng dẫn ${item.studentName}? Sinh viên sẽ chọn một giảng viên trong những người đã đồng ý. Chưa tạo Project và chưa trừ suất.`)) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    if (accepted) await supervisionApi.accept(item.id)
    else await supervisionApi.reject(item.id, reason)
    await load(); success.value = accepted ? 'Đã đồng ý hướng dẫn. Đang chờ sinh viên lựa chọn.' : 'Đã từ chối yêu cầu.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
async function selectOffer(item: LecturerRequestV06) {
  if (!window.confirm(`Chọn giảng viên ${item.lecturerName} cho đề tài “${item.draftTitle || item.topicTitle}”? Sau khi xác nhận, các lời mời khác sẽ bị hủy và Project sẽ được tạo.`)) return
  loading.value = true; error.value = ''; success.value = ''
  try {
    await supervisionApi.selectOffer(item.id)
    await load(); success.value = 'Đã chọn giảng viên hướng dẫn, khóa đề tài và tạo Project.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
watch(() => props.moduleId, () => void load())
watch(periodId, () => { if (props.moduleId === 'capacity' || props.moduleId === 'lecturerrequests') void load() })
onMounted(() => void load())
</script>
<template>
  <section class="intro"><span class="section-chip">Hướng dẫn · v0.6</span>
    <h2>{{moduleId === 'lecturerconfirmation' ? 'Xác nhận giảng viên' : moduleId === 'capacity' ? 'Sức chứa giảng viên' : moduleId === 'projects' ? 'Dự án của tôi' : 'Đăng ký đề tài & giảng viên hướng dẫn'}}</h2>
    <p>{{moduleId === 'lecturerconfirmation' ? 'Chọn một giảng viên đã đồng ý nhận bạn để xác nhận hướng dẫn chính thức.' : moduleId === 'projects' ? 'Project được tạo sau khi sinh viên chọn một giảng viên đã đồng ý hướng dẫn.' : 'Dữ liệu lấy từ API và SQL Server, được kiểm tra quyền tại Backend.'}}</p>
  </section>
  <p v-if="error" class="user-alert error" role="alert">{{error}}</p>
  <p v-if="success" class="user-alert success" role="status">{{success}}</p>
  <article v-if="moduleId !== 'projects' && moduleId !== 'lecturerconfirmation' && role !== 'Student'" class="panel users-panel">
    <div class="panel-heading"><h3>Đợt đăng ký</h3></div>
    <label>Chọn đợt
      <select v-model="periodId"><option value="" disabled>Chọn đợt</option>
        <option v-for="p in periods" :key="p.id" :value="p.id">{{p.name}} · {{p.isOpen ? 'Mở' : 'Đóng'}}</option>
      </select>
    </label>
    <p v-if="!periods.length" class="secondary-cell">Chưa có đợt đăng ký. Admin cần tạo đợt ở mục Đợt đăng ký.</p>
  </article>
  <article v-if="moduleId === 'capacity' && role === 'Lecturer' && periodId" class="panel user-editor">
    <h3>Giới hạn hướng dẫn chung cho mọi đợt</h3>
    <form class="user-form" @submit.prevent="saveCapacity">
      <label>Giới hạn sinh viên<input v-model.number="maxStudents" type="number" min="0" max="1000" step="1" required /></label>
      <p>Đang hướng dẫn ở tất cả các đợt: {{ownCapacity?.currentStudents ?? 0}} / {{ownCapacity?.maxStudents ?? 0}}. Còn {{ownCapacity?.remaining ?? 0}} suất. Giới hạn được kế thừa sang đợt mới.</p>
      <button class="action-primary" type="submit" :disabled="loading">Lưu sức chứa</button>
    </form>
  </article>
  <article v-if="moduleId === 'lecturerrequests' && role === 'Student'" class="panel user-editor">
    <h3>Đăng ký đề tài và chọn giảng viên cùng lúc</h3>
    <p>Bạn có thể gửi yêu cầu đến nhiều giảng viên. Giảng viên đồng ý trước, sau đó bạn chọn một người để chốt đề tài và tạo Project.</p>
    <form class="user-form" @submit.prevent="submitJoint">
      <label>Đề tài
        <select v-model="jointTopicId">
          <option value="new">Tự đề xuất đề tài mới</option>
          <option v-for="r in ownPendingProposals" :key="r.topicId" :value="r.topicId">Đề xuất đang chờ: {{r.draftTitle || r.topicTitle}} (gửi đến giảng viên khác)</option>
          <option v-for="t in publicTopics" :key="t.id" :value="t.id">{{t.title}}</option>
        </select>
      </label>
      <label>Giảng viên hướng dẫn
        <select v-model="jointLecturerId" required>
          <option value="" disabled>Chọn giảng viên</option>
          <option v-for="c in capacities.filter(c => c.remaining > 0)" :key="c.lecturerUserId" :value="c.lecturerUserId">{{c.lecturerName}} · Còn {{c.remaining}} chỗ</option>
        </select>
      </label>
      <p v-if="originalTopic" class="secondary-cell">Bản đề xuất riêng của bạn từ “{{originalTopic.title}}”. Bạn có thể sửa các ý bên dưới; đề tài gốc không thay đổi. Giảng viên sẽ nhận xét hoặc yêu cầu sửa trước khi đồng ý.</p>
      <details v-if="originalTopic"><summary>Xem nội dung đề tài gốc (chỉ xem)</summary>
        <p><strong>Tên:</strong> {{originalTopic.title}}</p>
        <p><strong>Mô tả:</strong> {{originalTopic.description}}</p>
        <p><strong>Mục tiêu:</strong> {{originalTopic.objective}}</p>
        <p><strong>Nội dung:</strong> {{originalTopic.expectedContent}}</p>
        <p><strong>Công nghệ:</strong> {{originalTopic.proposedTechnology}}</p>
      </details>
      <p v-if="jointTopicId !== 'new' && !originalTopic" class="secondary-cell">Bạn có thể điều chỉnh bản đề xuất này để gửi thêm cho giảng viên khác.</p>
      <template v-if="jointTopicId">
        <label>{{jointTopicId === 'new' ? 'Tên đề tài' : 'Tên đề tài đề xuất'}}<input v-model="jointDraft.title" required maxlength="300" /></label>
        <label>Mô tả<textarea v-model="jointDraft.description" required maxlength="4000" /></label>
        <label>Mục tiêu<textarea v-model="jointDraft.objective" required maxlength="2000" /></label>
        <label>Nội dung dự kiến<textarea v-model="jointDraft.expectedContent" required maxlength="2000" /></label>
        <label>Công nghệ dự kiến<textarea v-model="jointDraft.proposedTechnology" required maxlength="1000" /></label>
      </template>
      <p v-if="!activePeriod" class="secondary-cell">Chưa có đợt đăng ký đang mở. Admin cần mở đợt trước khi sinh viên gửi yêu cầu.</p>
      <button class="action-primary" type="submit" :disabled="loading || !activePeriod">Gửi yêu cầu đến giảng viên</button>
    </form>
  </article>
  <article v-if="moduleId === 'capacity'" class="panel users-panel">
    <h3>Sức chứa tổng hợp của giảng viên qua tất cả các đợt</h3>
    <div class="table-scroller"><table><thead><tr><th>Giảng viên</th><th>Chuyên môn</th><th>Đã nhận / Tối đa</th><th>Còn trống</th><th v-if="role === 'Student'">Thao tác</th></tr></thead><tbody>
      <tr v-if="!capacities.length"><td :colspan="role === 'Student' ? 5 : 4" class="empty-cell">Chưa có dữ liệu sức chứa giảng viên.</td></tr>
      <tr v-for="c in capacities" :key="c.lecturerUserId"><td>{{c.lecturerName}}</td><td>{{c.specialty || 'Chưa cập nhật'}}</td>
        <td>{{c.currentStudents}} / {{c.maxStudents}}</td><td>{{c.remaining}} · {{c.status}}</td>
        <td v-if="role === 'Student'"><button type="button" class="action-primary" :disabled="loading || c.remaining <= 0" @click="startLecturerRegistration(c)">Đăng ký hướng dẫn</button></td></tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId === 'lecturerconfirmation' && role === 'Student'" class="panel users-panel">
    <h3>Giảng viên đã đồng ý nhận bạn</h3>
    <p>Chọn một người để xác nhận hướng dẫn chính thức. Chỉ khi bạn xác nhận, hệ thống mới khóa đề tài, tính một suất hướng dẫn và tạo Project. Lời đồng ý có thể hết chỗ nếu giảng viên nhận sinh viên khác trước.</p>
    <div class="table-scroller"><table><thead><tr><th>Giảng viên</th><th>Đề tài</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="!offeredRequests.length"><td colspan="3" class="empty-cell">Chưa có giảng viên đồng ý nhận bạn.</td></tr>
      <tr v-for="offer in offeredRequests" :key="offer.id">
        <td>{{offer.lecturerName}}</td><td>{{offer.draftTitle || offer.topicTitle}}</td>
        <td><button type="button" class="action-primary" :disabled="loading" @click="selectOffer(offer)">Xác nhận giảng viên này</button></td>
      </tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId === 'lecturerrequests'" class="panel users-panel">
    <h3>{{role === 'Lecturer' ? 'Yêu cầu gửi đến bạn' : 'Yêu cầu đăng ký giảng viên'}}</h3>
    <div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Sinh viên</th><th>Giảng viên</th><th>Đợt</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="!requests.length"><td colspan="6" class="empty-cell">Chưa có yêu cầu đăng ký hướng dẫn.</td></tr>
      <tr v-for="r in requests" :key="r.id"><td>{{r.isCombined ? (r.draftTitle || r.topicTitle) : r.topicTitle}}
          <details v-if="r.isCombined"><summary>Xem bản đề xuất sinh viên</summary>
            <p><strong>Đề tài gốc:</strong> {{r.topicTitle}}</p>
            <p><strong>Tên đề tài đề xuất:</strong> {{r.draftTitle}}</p>
            <p><strong>Mô tả:</strong> {{r.draftDescription}}</p>
            <p><strong>Mục tiêu:</strong> {{r.draftObjective}}</p>
            <p><strong>Nội dung:</strong> {{r.draftExpectedContent}}</p>
            <p><strong>Công nghệ:</strong> {{r.draftProposedTechnology}}</p>
          </details></td><td>{{r.studentName}}</td><td>{{r.lecturerName}}</td><td>{{r.periodName}}</td>
        <td>{{r.status === 'REVISION_REQUIRED' ? 'CẦN CHỈNH SỬA' : r.status === 'OFFERED' ? 'GIẢNG VIÊN ĐÃ ĐỒNG Ý' : r.status}}<div v-if="r.rejectionReason" class="secondary-cell">Phản hồi: {{r.rejectionReason}}</div>
          <div v-if="r.isCombined" class="secondary-cell">Đăng ký kết hợp</div></td>
        <td class="actions"><template v-if="role === 'Lecturer' && r.status === 'PENDING'">
          <button type="button" :disabled="loading" @click="decide(r, true)">Đồng ý hướng dẫn</button>
          <button v-if="r.isCombined" type="button" :disabled="loading" @click="requestRevision(r)">Yêu cầu sửa</button>
          <button type="button" :disabled="loading" @click="decide(r, false)">Từ chối</button>
        </template>
        <button v-if="role === 'Student' && r.status === 'REVISION_REQUIRED' && r.isCombined"
          type="button" :disabled="loading" @click="startRevision(r)">Sửa và gửi lại</button>
        <button v-if="role === 'Student' && ['PENDING', 'OFFERED', 'REVISION_REQUIRED', 'REJECTED'].includes(r.status)"
          type="button" :disabled="loading" @click="cancelJoint(r)">{{r.isCombined ? 'Hủy đề xuất' : 'Hủy yêu cầu'}}</button></td></tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId === 'projects'" class="panel users-panel"><h3>Dự án đã được xác nhận</h3>
    <div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Sinh viên</th><th>Giảng viên hướng dẫn</th><th>Đợt</th><th>Trạng thái</th></tr></thead><tbody>
      <tr v-if="!projects.length"><td colspan="5" class="empty-cell">Chưa có Project. Cần hoàn tất đăng ký đề tài và xác nhận giảng viên.</td></tr>
      <tr v-for="p in projects" :key="p.id"><td>{{p.topicTitle}}</td><td>{{p.studentName}}</td><td>{{p.lecturerName}}</td><td>{{p.periodName}}</td><td>{{p.status}}</td></tr>
    </tbody></table></div>
  </article>
</template>
