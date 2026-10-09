<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { usersApi } from '../features/users/users.api'
import { useAuthStore } from '../stores/auth'
import { HttpError } from '../api/client'
import type { ManagedUser } from '../features/users/users.types'
const auth = useAuthStore()
const profile = ref<ManagedUser | null>(null)
const form = reactive({ fullName: '', faculty: '', specialty: '' })
const saving = ref(false)
const loading = ref(true)
const error = ref('')
const success = ref('')
function message(err: unknown) { return err instanceof HttpError ? err.payload?.message || err.message : err instanceof Error ? err.message : 'Có lỗi xảy ra.' }
onMounted(async () => {
  try {
    profile.value = await usersApi.ownProfile()
    form.fullName = profile.value.fullName; form.faculty = profile.value.faculty || ''; form.specialty = profile.value.specialty || ''
  } catch (err) { error.value = message(err) }
  finally { loading.value = false }
})
async function save() {
  saving.value = true; error.value = ''; success.value = ''
  try {
    profile.value = await usersApi.updateOwnProfile({ ...form })
    if (auth.currentUser && profile.value) auth.currentUser.fullName = profile.value.fullName
    success.value = 'Hồ sơ đã được cập nhật.'
  } catch (err) { error.value = message(err) }
  finally { saving.value = false }
}
</script>
<template>
  <section class="intro"><span class="section-chip">Tài khoản · v0.4</span><h2>Hồ sơ của tôi</h2><p>Bạn chỉ được sửa họ tên và thông tin hồ sơ của chính mình.</p></section>
  <p v-if="error" class="user-alert error" role="alert">{{ error }}</p><p v-if="success" class="user-alert success" role="status">{{ success }}</p>
  <article class="panel user-editor"><p v-if="loading">Đang tải hồ sơ...</p>
    <form v-else-if="profile" class="user-form" @submit.prevent="save">
      <label>Họ tên<input v-model.trim="form.fullName" maxlength="200" required /></label>
      <label>Email (chỉ Admin được thay đổi)<input :value="profile.email" disabled /></label>
      <label v-if="profile.role === 'Student'">Mã sinh viên<input :value="profile.studentCode || ''" disabled /></label>
      <label v-if="profile.role === 'Student'">Khoa<input v-model.trim="form.faculty" maxlength="200" /></label>
      <label v-if="profile.role === 'Lecturer'">Chuyên môn<input v-model.trim="form.specialty" maxlength="300" /></label>
      <button type="submit" class="action-primary" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Cập nhật hồ sơ' }}</button>
    </form>
  </article>
</template>
