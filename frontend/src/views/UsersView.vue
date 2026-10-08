<script setup lang="ts">
import { nextTick, onMounted, reactive, ref } from 'vue'
import { usersApi } from '../features/users/users.api'
import { HttpError } from '../api/client'
import type { CreateUserPayload, ManagedRole, ManagedUser, PaginatedUsers } from '../features/users/users.types'

const listing = ref<PaginatedUsers>({ items: [], total: 0, page: 1, pageSize: 20 })
const filterRole = ref('')
const search = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const success = ref('')
const editor = ref<'none' | 'create' | 'edit'>('none')
const editorPanel = ref<HTMLElement | null>(null)
function focusEditor() { void nextTick(() => editorPanel.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })) }
const editingId = ref('')
const form = reactive<CreateUserPayload>({ role: 'Student', fullName: '', email: '', password: '', studentCode: '', faculty: '', specialty: '' })
function message(err: unknown) {
  if (err instanceof HttpError) return err.payload?.message || `API error (${err.status})`
  return err instanceof Error ? err.message : 'Có lỗi khi xử lý dữ liệu.'
}
async function reload(page = 1) {
  loading.value = true; error.value = ''
  try { listing.value = await usersApi.list({ role: filterRole.value, search: search.value.trim(), page, pageSize: 20 }) }
  catch (err) { error.value = message(err) }
  finally { loading.value = false }
}
function clearForm() {
  form.role = 'Student'; form.fullName = ''; form.email = ''; form.password = ''
  form.studentCode = ''; form.faculty = ''; form.specialty = ''
}
function startCreate() { clearForm(); editingId.value = ''; editor.value = 'create'; error.value = ''; success.value = ''; focusEditor() }
function startEdit(user: ManagedUser) {
  clearForm(); editingId.value = user.id; editor.value = 'edit'; error.value = ''; success.value = ''
  form.role = user.role; form.fullName = user.fullName; form.email = user.email
  form.studentCode = user.studentCode || ''; form.faculty = user.faculty || ''; form.specialty = user.specialty || ''; focusEditor()
}
async function save() {
  saving.value = true; error.value = ''; success.value = ''
  try {
    if (editor.value === 'create') {
      if (form.password.length < 12) { error.value = 'Mật khẩu cần ít nhất 12 ký tự.'; return }
      await usersApi.create({ ...form })
      success.value = 'Tạo tài khoản thành công.'
    } else if (editor.value === 'edit') {
      await usersApi.update(editingId.value, { fullName: form.fullName, email: form.email,
        studentCode: form.studentCode, faculty: form.faculty, specialty: form.specialty })
      success.value = 'Cập nhật tài khoản thành công.'
    }
    form.password = ''; editor.value = 'none'; await reload(listing.value.page)
  } catch (err) { error.value = message(err) }
  finally { saving.value = false }
}
async function toggle(user: ManagedUser) {
  if (!window.confirm(`${user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt lại'} tài khoản ${user.fullName}?`)) return
  error.value = ''; success.value = ''
  try { await usersApi.setStatus(user.id, !user.isActive); success.value = 'Đã thay đổi trạng thái tài khoản.'; await reload(listing.value.page) }
  catch (err) { error.value = message(err) }
}
function setRole(next: string) { filterRole.value = next; void reload() }
onMounted(() => void reload())
</script>
<template>
  <section class="intro"><span class="section-chip">Quản trị · FR-03, FR-04</span><h2>Quản lý người dùng</h2><p>Admin tạo, cập nhật, tìm kiếm và vô hiệu hóa tài khoản Student/Lecturer. Dữ liệu lấy từ API thật.</p></section>
  <div v-if="error" class="user-alert error" role="alert">{{ error }}</div>
  <div v-if="success" class="user-alert success" role="status">{{ success }}</div>
  <article class="panel users-panel">
    <div class="panel-heading"><h3>Danh sách tài khoản ({{ listing.total }})</h3><button type="button" class="action-primary" @click="startCreate">+ Tạo tài khoản</button></div>
    <form class="user-filters" @submit.prevent="reload()">
      <label>Vai trò<select :value="filterRole" @change="setRole(($event.target as HTMLSelectElement).value)"><option value="">Tất cả</option><option value="Student">Sinh viên</option><option value="Lecturer">Giảng viên</option></select></label>
      <label>Tìm kiếm<input v-model="search" maxlength="200" placeholder="Họ tên, email, mã sinh viên" /></label>
      <button type="submit" class="action-secondary">Tìm kiếm</button>
    </form>
    <div class="table-scroller"><table><thead><tr><th>Họ tên / Email</th><th>Vai trò</th><th>Mã SV / Chuyên môn</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
      <tr v-if="loading"><td colspan="5" class="empty-cell">Đang tải danh sách...</td></tr>
      <tr v-else-if="!listing.items.length"><td colspan="5" class="empty-cell">Không có tài khoản phù hợp.</td></tr>
      <tr v-for="user in listing.items" :key="user.id"><td><strong>{{ user.fullName }}</strong><div class="secondary-cell">{{ user.email }}</div></td>
        <td>{{ user.role === 'Student' ? 'Sinh viên' : 'Giảng viên' }}</td><td>{{ user.role === 'Student' ? user.studentCode : user.specialty || 'Chưa cập nhật' }}</td>
        <td><span :class="['status-label', user.isActive ? 'active' : 'inactive']">{{ user.isActive ? 'Hoạt động' : 'Đã khóa' }}</span></td>
        <td class="actions"><button type="button" @click="startEdit(user)">Sửa</button><button type="button" @click="toggle(user)">{{ user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt' }}</button></td></tr>
    </tbody></table></div>
    <div class="user-pagination"><span>Trang {{ listing.page }} / {{ Math.max(1, Math.ceil(listing.total / listing.pageSize)) }}</span>
      <button type="button" :disabled="listing.page <= 1 || loading" @click="reload(listing.page - 1)">Trước</button>
      <button type="button" :disabled="listing.page * listing.pageSize >= listing.total || loading" @click="reload(listing.page + 1)">Sau</button></div>
  </article>
  <article v-if="editor !== 'none'" ref="editorPanel" class="panel user-editor">
    <div class="panel-heading"><h3>{{ editor === 'create' ? 'Tạo tài khoản' : 'Chỉnh sửa tài khoản' }}</h3><button type="button" class="action-secondary" @click="editor = 'none'; form.password = ''">Đóng</button></div>
    <form class="user-form" @submit.prevent="save">
      <label v-if="editor === 'create'">Vai trò<select v-model="form.role"><option value="Student">Sinh viên</option><option value="Lecturer">Giảng viên</option></select></label>
      <p v-else>Vai trò: <strong>{{ form.role === 'Student' ? 'Sinh viên' : 'Giảng viên' }}</strong> (không cho phép thay đổi)</p>
      <label>Họ tên<input v-model.trim="form.fullName" maxlength="200" required /></label>
      <label>Email<input v-model.trim="form.email" type="email" maxlength="256" required /></label>
      <label v-if="editor === 'create'">Mật khẩu ban đầu<input v-model="form.password" type="password" minlength="12" maxlength="128" required autocomplete="new-password" /></label>
      <label v-if="form.role === 'Student'">Mã sinh viên<input v-model.trim="form.studentCode" maxlength="40" required /></label>
      <label v-if="form.role === 'Student'">Khoa<input v-model.trim="form.faculty" maxlength="200" /></label>
      <label v-if="form.role === 'Lecturer'">Chuyên môn<input v-model.trim="form.specialty" maxlength="300" /></label>
      <button type="submit" class="action-primary" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu tài khoản' }}</button>
    </form>
    <p class="disclaimer">Chỉ quản lý Student/Lecturer. Mật khẩu được xử lý và hash phía Backend, không hiển thị trong danh sách.</p>
  </article>
</template>
