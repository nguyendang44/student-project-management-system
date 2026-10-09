<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { usersApi } from '../features/users/users.api'
import { HttpError } from '../api/client'
import type { ManagedUser } from '../features/users/users.types'

const profile = ref<ManagedUser | null>(null)
const loading = ref(true)
const error = ref('')
function message(err: unknown) {
  return err instanceof HttpError ? err.payload?.message || err.message : err instanceof Error ? err.message : 'Có lỗi xảy ra.'
}
onMounted(async () => {
  try { profile.value = await usersApi.ownProfile() }
  catch (err) { error.value = message(err) }
  finally { loading.value = false }
})
</script>

<template>
  <section class="intro">
    <span class="section-chip">Tài khoản</span>
    <h2>Hồ sơ của tôi</h2>
    <p>Thông tin hồ sơ chỉ được xem. Chỉ Admin được chỉnh sửa thông tin tài khoản trong mục Quản lý người dùng.</p>
  </section>
  <p v-if="error" class="user-alert error" role="alert">{{ error }}</p>
  <article class="panel user-editor">
    <p v-if="loading">Đang tải hồ sơ...</p>
    <div v-else-if="profile" class="user-form">
      <label>Họ tên<input :value="profile.fullName" readonly /></label>
      <label>Email<input :value="profile.email" readonly /></label>
      <label v-if="profile.role === 'Student'">Mã sinh viên<input :value="profile.studentCode || ''" readonly /></label>
      <label v-if="profile.role === 'Student'">Khoa<input :value="profile.faculty || ''" readonly /></label>
      <label v-if="profile.role === 'Lecturer'">Chuyên môn<input :value="profile.specialty || ''" readonly /></label>
    </div>
  </article>
</template>
