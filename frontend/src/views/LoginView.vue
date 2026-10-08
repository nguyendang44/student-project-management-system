<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { HttpError } from '../api/client'
const email = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
async function submit() {
  error.value = ''
  if (!email.value.trim() || !password.value) { error.value = 'Vui lòng nhập email và mật khẩu.'; return }
  busy.value = true
  try {
    await auth.login(email.value, password.value)
    const next = typeof route.query.next === 'string' && route.query.next.startsWith('/') && !route.query.next.startsWith('//')
      ? route.query.next : '/dashboard'
    await router.replace(next)
  } catch (err) {
    error.value = err instanceof HttpError && err.status === 401 ? 'Email hoặc mật khẩu không đúng.'
      : err instanceof Error ? err.message : 'Không thể đăng nhập. Vui lòng thử lại.'
  } finally { busy.value = false }
}
</script>
<template>
  <div class="login-background"><form class="login-panel" @submit.prevent="submit">
    <span class="section-chip">AUTHENTICATION v0.3</span><h1>Đăng nhập</h1>
    <p>Đăng nhập bằng tài khoản đã được tạo trong cơ sở dữ liệu. Quyền truy cập được xác minh tại Backend.</p>
    <label>Email<input v-model.trim="email" type="email" placeholder="email@example.com" autocomplete="username" required maxlength="256" /></label>
    <label>Mật khẩu<input v-model="password" type="password" placeholder="Nhập mật khẩu" autocomplete="current-password" required /></label>
    <p v-if="error" class="auth-error" role="alert">{{ error }}</p>
    <button class="primary-button" type="submit" :disabled="busy">{{ busy ? 'Đang đăng nhập...' : 'Đăng nhập' }}</button>
    <p class="login-footnote">Phiên đăng nhập được lưu trong bộ nhớ. Tải lại trang sẽ yêu cầu đăng nhập lại.</p>
  </form></div>
</template>
