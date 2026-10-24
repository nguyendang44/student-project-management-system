<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { v08Api } from '../features/v08/v08.api'
// V080_AUTOMATION_GUIDANCE
import { useRoute, useRouter } from 'vue-router'
import { modules } from '../app/modules'
import { useAuthStore } from '../stores/auth'
const unreadV08 = ref(0)
let v08Timer: ReturnType<typeof setInterval> | undefined
async function reloadUnreadV08() {
  try { unreadV08.value = (await v08Api.notifications()).unreadCount }
  catch { /* notifications may not be upgraded yet; don't break existing site */ }
}
onMounted(() => { void reloadUnreadV08(); v08Timer = setInterval(() => { void reloadUnreadV08() }, 60000) })
onUnmounted(() => { if (v08Timer) clearInterval(v08Timer) })
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const groups = computed(() => {
  const visible = modules.filter(m => auth.role && m.roles.includes(auth.role))
  return [...new Set(visible.map(m => m.group))].map(name => ({ name, items: visible.filter(m => m.group === name) }))
})
async function signOut() {
  try { await auth.logout() }
  catch { auth.clear() }
  await router.replace('/login')
}
</script>
<template>
  <div class="app-shell">
    <aside class="sidebar">
      <RouterLink to="/dashboard" class="brand"><span class="brand-mark">SP</span><span><strong>StudentProjects</strong><small>Management system</small></span></RouterLink>
      <div class="sidebar-scroll"><nav v-for="group in groups" :key="group.name" :aria-label="group.name" class="nav-group">
        <p class="nav-title">{{ group.name }}</p>
        <RouterLink v-for="item in group.items" :key="item.id" :to="item.path" class="nav-link">{{ item.title }}</RouterLink>
      </nav></div>
      <div class="sidebar-footer"><span class="version-tag">TOPIC MANAGEMENT v0.5</span><p>Auth, User Management, Topic Management dùng API thật; module còn lại là skeleton</p></div>
    </aside>
    <div class="main-column">
      <header class="topbar"><div><div class="eyebrow">HỆ THỐNG QUẢN LÝ DỰ ÁN SINH VIÊN</div><h1>{{ route.meta.title || 'Dashboard' }}</h1></div>
        <div class="account-summary"><RouterLink to="/notifications" title="Thông báo mới">Thông báo <strong v-if="unreadV08">({{ unreadV08 }})</strong></RouterLink><RouterLink v-if="auth.role !== 'Admin'" class="profile-link" to="/profile">Hồ sơ của tôi</RouterLink><div><strong>{{ auth.currentUser?.fullName }}</strong><small>{{ auth.currentUser?.role }} · {{ auth.currentUser?.email }}</small></div><button type="button" @click="signOut">Đăng xuất</button></div>
      </header>
      <main class="main-content"><RouterView /></main>
    </div>
  </div>
</template>
