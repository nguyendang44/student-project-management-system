<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { HttpError } from '../api/client'
import { moduleById, type ModuleId } from '../app/modules'
import { useAuthStore } from '../stores/auth'
import { topicsApi } from '../features/topics/topics.api'
import { proposalsApi } from '../features/proposals/proposals.api'
import { periodsApi } from '../features/periods/periods.api'
import { topicregistrationsApi } from '../features/topicregistrations/topicregistrations.api'
import type { Topic, TopicPayload, TopicRegistration, RegistrationPeriod } from '../features/topics/topics.types'

const props = defineProps<{moduleId: ModuleId}>()
const auth = useAuthStore()
const router = useRouter()
const moduleInfo = computed(() => moduleById(props.moduleId))
const role = computed(() => auth.role)
const busy = ref(false), error = ref(''), success = ref('')
const topics = ref<Topic[]>([]), proposals = ref<Topic[]>([]), periods = ref<RegistrationPeriod[]>([])
const registrations = ref<TopicRegistration[]>([]), mineTopics = ref<Topic[]>([])
const rejectionNotes = reactive<Record<string,string>>({})
const total = ref(0), page = ref(1), search = ref('')
const editor = ref<'none'|'create'|'edit'|'propose'>('none'), editedId = ref('')
const values = reactive<TopicPayload>({title:'',description:'',objective:'',expectedContent:'',proposedTechnology:''})
const periodForm = reactive({ name:'', startsAt:'', endsAt:'', isOpen:true })
const canManage = (t:Topic) => role.value === 'Admin' ||
 (role.value === 'Lecturer' && t.proposedByUserId === auth.currentUser?.userId) ||
 (role.value === 'Student' && t.proposedByUserId === auth.currentUser?.userId && ['DRAFT','REJECTED'].includes(t.status))
const canDeleteTopic = (t:Topic) => t.canDelete && (role.value === 'Admin' ||
  (role.value === 'Lecturer' && t.proposedByUserId === auth.currentUser?.userId))
const canToggleRegistration = (t:Topic) => !t.reservedForStudentUserId &&
 (role.value === 'Admin' || role.value === 'Lecturer' &&
 t.proposedByUserId === auth.currentUser?.userId) &&
 (t.status === 'CANCELLED' || t.status === 'PUBLISHED' ||
  t.status === 'APPROVED')
const isReservedForMe = (t:Topic) => !!t.reservedForStudentUserId && t.reservedForStudentUserId === auth.currentUser?.userId
const hasPendingRequest = (t:Topic) => registrations.value.some(r => r.topicId === t.id && r.status === 'PENDING')
function errMessage(err:unknown){return err instanceof HttpError ? (err.payload?.message || `API error (${err.status})`) : err instanceof Error ? err.message : 'Lỗi khi truy cập dữ liệu.'}
function clearAlert(){error.value='';success.value=''}
async function reload(){
  busy.value=true; error.value=''
  try {
    if (props.moduleId === 'topics') { const data=await topicsApi.list(search.value,page.value);topics.value=data.items;total.value=data.total;if(role.value==='Student'){registrations.value=await topicregistrationsApi.list();mineTopics.value=await topicsApi.mine()} }
    if (props.moduleId === 'proposals') proposals.value=await proposalsApi.list()
    if (props.moduleId === 'topicregistrations') { registrations.value=await topicregistrationsApi.list(); mineTopics.value=role.value==='Student'?await topicsApi.mine():[] }
    if (props.moduleId === 'periods') periods.value=await periodsApi.list()
  } catch(e){error.value=errMessage(e)} finally {busy.value=false}
}
function startCreate(mode:'create'|'propose'){
  if (role.value === 'Student') { void router.push('/lecturer-requests'); return }
  clearAlert(); editedId.value='';editor.value=mode
  Object.assign(values,{title:'',description:'',objective:'',expectedContent:'',proposedTechnology:''})
}
function startEdit(item:Topic){
  clearAlert(); editor.value='edit';editedId.value=item.id
  Object.assign(values,{title:item.title,description:item.description,objective:item.objective||'',expectedContent:item.expectedContent||'',proposedTechnology:item.proposedTechnology||''})
}
async function saveTopic(){
  busy.value=true;clearAlert()
  try {
    if(editor.value==='edit') await topicsApi.update(editedId.value,{...values})
    else if(editor.value==='propose') await proposalsApi.create({...values})
    else await topicsApi.create({...values})
    success.value='Đã lưu đề tài thành công.'; editor.value='none';await reload()
  }catch(e){error.value=errMessage(e)}finally{busy.value=false}
}
async function showHistory(t:Topic){clearAlert();try{const data=await topicsApi.history(t.id);rejectionNotes[t.id]=data.filter(x=>x.toStatus==='REJECTED').at(-1)?.reason||'Không có lý do từ chối.'}catch(e){error.value=errMessage(e)}}
async function toggleTopic(t:Topic){busy.value=true;clearAlert();try{await topicsApi.open(t.id,!t.isRegistrationOpen);success.value='Đã cập nhật trạng thái đăng ký đề tài.';await reload()}catch(e){error.value=errMessage(e)}finally{busy.value=false}}
async function deleteTopic(t:Topic){
  if(!canDeleteTopic(t))return
  if(!window.confirm(`Xóa vĩnh viễn đề tài “${t.title}”? Các lịch sử đăng ký của đề tài cũng sẽ bị xóa vĩnh viễn. Không thể xóa đề tài đang có người sở hữu hoặc Project.`))return
  busy.value=true;clearAlert()
  try{await topicsApi.remove(t.id);editor.value='none';
    if(topics.value.length===1&&page.value>1)page.value--;
    await reload();success.value='Đã xóa đề tài.'}
  catch(e){error.value=errMessage(e)}finally{busy.value=false}
}
function startRegister(t:Topic){
  clearAlert()
  // Direct students to the joint application form; do not create a standalone topic registration.
  void router.push({ path:'/lecturer-requests', query:{ topicId:t.id } })
}
async function decideRegistration(r:TopicRegistration,accept:boolean){
  if(!window.confirm(accept?`Chấp nhận sinh viên ${r.studentName} và khóa đề tài ${r.topicTitle}? Toàn bộ đăng ký khác (kể cả lịch sử đã hủy hoặc từ chối) của sinh viên sẽ bị xóa vĩnh viễn.`:`Từ chối yêu cầu của ${r.studentName}?`))return
  busy.value=true;clearAlert()
  try{if(accept)await topicregistrationsApi.accept(r.id);else await topicregistrationsApi.reject(r.id)
    await reload();success.value=accept?'Đã khóa đề tài cho sinh viên được chấp nhận.':'Đã từ chối đăng ký.'
  }catch(e){error.value=errMessage(e)}finally{busy.value=false}
}
async function withdrawTopic(t:Topic){
  const selfCreated = t.proposedByUserId === auth.currentUser?.userId && t.status === 'APPROVED'
  if(!window.confirm(selfCreated
    ? `Từ bỏ đề tài tự tạo “${t.title}”? Đề tài và các yêu cầu liên quan sẽ bị xóa vĩnh viễn, KHÔNG đưa vào danh sách đề tài dự bị. Nếu Project đã bắt đầu thực hiện, thao tác sẽ bị từ chối.`
    : `Từ bỏ đề tài “${t.title}”? Đề tài có sẵn sẽ được mở lại cho sinh viên khác đăng ký (nếu chưa có Project).`))return
  busy.value=true;clearAlert()
  try{const result=await topicsApi.withdraw(t.id);await reload();success.value=result.status==='DELETED'?'Đã xóa đề tài tự tạo và giải phóng quyền sở hữu.':'Đã từ bỏ đề tài có sẵn. Đề tài đã tự động mở đăng ký lại.'}
  catch(e){error.value=errMessage(e)}finally{busy.value=false}
}
async function cancelRegistration(r:TopicRegistration){if(!window.confirm('Hủy yêu cầu đăng ký đang chờ?'))return
  busy.value=true;clearAlert();try{await topicregistrationsApi.cancel(r.id);success.value='Đã hủy yêu cầu đăng ký.';await reload()}catch(e){error.value=errMessage(e)}finally{busy.value=false}
}
async function createPeriod(){busy.value=true;clearAlert();try{
  if(!periodForm.startsAt||!periodForm.endsAt){error.value='Chọn thời gian bắt đầu và kết thúc.';return}
  await periodsApi.create({name:periodForm.name,startsAt:new Date(periodForm.startsAt).toISOString(),endsAt:new Date(periodForm.endsAt).toISOString(),isOpen:periodForm.isOpen})
  Object.assign(periodForm,{name:'',startsAt:'',endsAt:'',isOpen:true});success.value='Đã tạo đợt đăng ký.';await reload()
}catch(e){error.value=errMessage(e)}finally{busy.value=false}}
async function togglePeriod(p:RegistrationPeriod){busy.value=true;clearAlert();try{await periodsApi.setState(p.id,!p.isOpen);success.value='Đã cập nhật đợt đăng ký.';await reload()}catch(e){error.value=errMessage(e)}finally{busy.value=false}}
function formatDate(iso:string){return new Date(iso).toLocaleString('vi-VN')}
watch(()=>props.moduleId,()=>{editor.value='none';void reload()})
onMounted(()=>void reload())
</script>
<template>
  <section class="intro"><span class="section-chip">{{moduleInfo.group}} · {{moduleInfo.requirements}} · v0.5</span><h2>{{moduleInfo.title}}</h2><p>{{moduleInfo.description}}. Dữ liệu lấy từ API và SQL Server.</p></section>
  <div v-if="error" class="user-alert error" role="alert">{{error}}</div>
  <div v-if="success" class="user-alert success" role="status">{{success}}</div>
  <article v-if="moduleId==='topics'" class="panel users-panel">
    <div class="panel-heading"><h3>Đề tài ({{total}})</h3><button type="button" class="action-primary" @click="startCreate('create')">+ {{role==='Student'?'Tự đề xuất + chọn giảng viên':'Tạo đề tài'}}</button></div>
    <form class="user-filters" @submit.prevent="page=1;reload()"><label>Tìm kiếm<input v-model.trim="search" maxlength="200" placeholder="Tên đề tài" /></label><button type="submit" class="action-secondary">Tìm kiếm</button></form>
    <div class="table-scroller"><table><thead><tr><th>Đề tài / người đề xuất</th><th>Trạng thái</th><th>Đăng ký</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="busy"><td colspan="4">Đang tải...</td></tr><tr v-else-if="!topics.length"><td colspan="4" class="empty-cell">Chưa có đề tài.</td></tr>
      <tr v-for="t in topics" :key="t.id"><td><strong>{{t.title}}</strong><div class="secondary-cell">{{t.proposedBy}}</div><div class="secondary-cell">{{t.description}}</div></td><td>{{t.status==='PUBLISHED'?'ĐÃ CÔNG BỐ':t.status==='CANCELLED'?'ĐÃ TỪ BỎ':t.status}}<div v-if="isReservedForMe(t)" class="secondary-cell">Đề tài của tôi · Đã khóa</div></td><td>{{t.reservedForStudentUserId?(isReservedForMe(t)?'Dành riêng cho bạn':'Đã có sinh viên'):(t.isRegistrationOpen?'Đang mở':'Đóng')}}</td>
        <td class="actions"><button v-if="canManage(t)" type="button" @click="startEdit(t)">Sửa</button>
        <button v-if="canDeleteTopic(t)" type="button" :disabled="busy" @click="deleteTopic(t)">Xóa</button>
        <button v-if="canToggleRegistration(t)" type="button" :disabled="busy" @click="toggleTopic(t)">{{t.isRegistrationOpen?'Đóng đăng ký':'Mở đăng ký'}}</button>
        <span v-if="role==='Student'&&t.proposedByUserId===auth.currentUser?.userId&&['DRAFT','REJECTED'].includes(t.status)" class="secondary-cell">Đề xuất cũ · Tạo đăng ký kết hợp để xét duyệt</span>
        <span v-if="role==='Student'&&hasPendingRequest(t)" class="secondary-cell">Đã đăng ký · Chờ duyệt</span>
        <button v-if="role==='Student'&&['APPROVED','PUBLISHED'].includes(t.status)&&t.isRegistrationOpen&&!t.reservedForStudentUserId&&!hasPendingRequest(t)&&!mineTopics.length" type="button" @click="startRegister(t)">Đăng ký</button></td></tr>
    </tbody></table></div><div class="user-pagination"><span>Trang {{page}} / {{Math.max(1,Math.ceil(total/20))}}</span><button type="button" :disabled="page<=1||busy" @click="page--;reload()">Trước</button><button type="button" :disabled="page*20>=total||busy" @click="page++;reload()">Sau</button></div>
  </article>
  <article v-if="moduleId==='proposals'" class="panel users-panel"><p class="secondary-cell">Đề tài mới chỉ cần giảng viên hướng dẫn được chọn chấp nhận một lần. Trang này chỉ hiển thị dữ liệu cũ.</p><div class="panel-heading"><h3>Đề xuất cũ (chỉ xem)</h3><button v-if="role==='Student'" class="action-primary" type="button" @click="startCreate('propose')">Đề xuất + chọn giảng viên</button></div>
    <div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Người đề xuất</th><th>Trạng thái</th><th>Xử lý</th></tr></thead><tbody>
      <tr v-if="!proposals.length"><td colspan="4" class="empty-cell">Chưa có đề xuất.</td></tr>
      <tr v-for="t in proposals" :key="t.id"><td><strong>{{t.title}}</strong><div class="secondary-cell">{{t.description}}</div></td><td>{{t.proposedBy}}</td><td>{{t.status}}<div v-if="rejectionNotes[t.id]" class="secondary-cell">Lý do: {{rejectionNotes[t.id]}}</div></td>
        <td class="actions"><button v-if="role==='Student'&&t.status==='REJECTED'" @click="showHistory(t)">Xem lý do</button><span class="secondary-cell">Yêu cầu mới xử lý tại Đăng ký đề tài &amp; giảng viên hướng dẫn</span></td></tr>
    </tbody></table></div>
  </article>
  <article v-if="moduleId==='topicregistrations'&&role==='Student'&&mineTopics.length" class="panel users-panel"><h3>Đề tài đã dành riêng cho bạn</h3><p v-if="mineTopics.length>1" class="user-alert error" role="alert">Tài khoản hiện đang có nhiều đề tài được duyệt. Mỗi sinh viên chỉ được giữ một đề tài. Hãy chọn đề tài muốn giữ và nhấn “Từ bỏ đề tài” ở đề tài còn lại. Hệ thống không tự xóa dữ liệu của bạn.</p><div v-for="t in mineTopics" :key="t.id"><strong>{{t.title}}</strong><span class="secondary-cell"> · Đã khóa · {{t.proposedByUserId===auth.currentUser?.userId?'Đề xuất của bạn':'Giảng viên đã chấp nhận'}}</span> <button v-if="['APPROVED','PUBLISHED'].includes(t.status)" class="action-secondary" type="button" :disabled="busy" @click="withdrawTopic(t)">Từ bỏ đề tài</button></div></article>
  <article v-if="moduleId==='topicregistrations'" class="panel users-panel"><h3>Yêu cầu đăng ký đề tài</h3><p v-if="role==='Student'" class="secondary-cell">Bạn có thể đăng ký nhiều đề tài trong cùng đợt. Khi giảng viên chấp nhận một đề tài, hệ thống chỉ giữ đăng ký ACCEPTED và xóa toàn bộ các đăng ký còn lại của bạn.</p><div class="table-scroller"><table><thead><tr><th>Đề tài</th><th>Sinh viên</th><th>Đợt</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
    <tr v-if="!registrations.length"><td colspan="5" class="empty-cell">Chưa có yêu cầu. Sinh viên đăng ký từ trang Danh sách đề tài.</td></tr><tr v-for="r in registrations" :key="r.id"><td>{{r.topicTitle}}</td><td>{{r.studentName}}</td><td>{{r.periodName}}</td><td>{{r.status}}<span v-if="r.status==='ACCEPTED'" class="secondary-cell"> · Đã khóa</span></td><td class="actions"><button v-if="role==='Student'&&r.status==='PENDING'" class="action-secondary" @click="cancelRegistration(r)">Hủy yêu cầu</button><button v-if="role==='Lecturer'&&r.status==='PENDING'" :disabled="busy" @click="decideRegistration(r,true)">Chấp nhận & khóa</button><button v-if="role==='Lecturer'&&r.status==='PENDING'" :disabled="busy" @click="decideRegistration(r,false)">Từ chối</button></td></tr>
  </tbody></table></div></article>
  <article v-if="moduleId==='periods'" class="panel users-panel"><h3>Đợt đăng ký</h3><div class="table-scroller"><table><thead><tr><th>Tên đợt</th><th>Bắt đầu</th><th>Kết thúc</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody><tr v-if="!periods.length"><td colspan="5" class="empty-cell">Chưa có đợt đăng ký.</td></tr><tr v-for="p in periods" :key="p.id"><td>{{p.name}}</td><td>{{formatDate(p.startsAt)}}</td><td>{{formatDate(p.endsAt)}}</td><td>{{p.isOpen?'Đang mở':'Đóng'}}</td><td><button class="action-secondary" type="button" @click="togglePeriod(p)">{{p.isOpen?'Đóng':'Mở'}}</button></td></tr></tbody></table></div>
    <form class="user-form" @submit.prevent="createPeriod"><label>Tên đợt<input v-model.trim="periodForm.name" maxlength="200" required /></label><label>Bắt đầu<input v-model="periodForm.startsAt" type="datetime-local" required /></label><label>Kết thúc<input v-model="periodForm.endsAt" type="datetime-local" required /></label><label>Trạng thái<select v-model="periodForm.isOpen"><option :value="true">Mở</option><option :value="false">Đóng</option></select></label><button class="action-primary" :disabled="busy" type="submit">Tạo đợt</button></form>
  </article>
  <article v-if="editor!=='none'" class="panel user-editor"><div class="panel-heading"><h3>{{editor==='edit'?'Chỉnh sửa đề tài':editor==='propose'?'Đề xuất đề tài':'Tạo đề tài'}}</h3><button class="action-secondary" type="button" @click="editor='none'">Đóng</button></div>
    <form class="user-form" @submit.prevent="saveTopic"><label>Tên đề tài<input v-model.trim="values.title" maxlength="300" required /></label><label>Mô tả<textarea v-model.trim="values.description" maxlength="4000" required /></label><label>Mục tiêu<textarea v-model.trim="values.objective" maxlength="2000" :required="editor==='propose'" /></label><label>Nội dung dự kiến<textarea v-model.trim="values.expectedContent" maxlength="2000" :required="editor==='propose'" /></label><label>Công nghệ dự kiến<input v-model.trim="values.proposedTechnology" maxlength="1000" :required="editor==='propose'" /></label><button class="action-primary" type="submit" :disabled="busy">{{busy?'Đang lưu...':'Lưu đề tài'}}</button></form>
  </article>

</template>
