<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { modules } from '../app/modules'
import { useAuthStore } from '../stores/auth'
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
      <div class="sidebar-footer"><span class="version-tag">AUTH v0.3</span><p>Đăng nhập và phân quyền thật · Các module khác vẫn là skeleton</p></div>
    </aside>
    <div class="main-column">
      <header class="topbar"><div><div class="eyebrow">HỆ THỐNG QUẢN LÝ DỰ ÁN SINH VIÊN</div><h1>{{ route.meta.title || 'Dashboard' }}</h1></div>
        <div class="account-summary"><div><strong>{{ auth.currentUser?.fullName }}</strong><small>{{ auth.currentUser?.role }} · {{ auth.currentUser?.email }}</small></div><button type="button" @click="signOut">Đăng xuất</button></div>
      </header>
      <main class="main-content"><div class="notice"><strong>Authentication đã hoạt động.</strong> Các trang nghiệp vụ vẫn là skeleton và các API tương ứng trả về 501.</div><RouterView /></main>
    </div>
  </div>
</template>
