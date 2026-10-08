<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { modules } from '../app/modules'
import { usePreviewStore, type Role } from '../stores/preview'
const route = useRoute()
const router = useRouter()
const store = usePreviewStore()
const groups = computed(() => {
  const visible = modules.filter((m) => m.roles.includes(store.role))
  return [...new Set(visible.map((m) => m.group))].map((name) => ({ name, items: visible.filter((m) => m.group === name) }))
})
function onRoleChange(event: Event) {
  store.role = (event.target as HTMLSelectElement).value as Role
  const active = modules.find((m) => m.path === route.path)
  if (active && !active.roles.includes(store.role)) void router.push('/dashboard')
}
</script>
<template>
  <div class="app-shell">
    <aside class="sidebar">
      <RouterLink to="/dashboard" class="brand">
        <span class="brand-mark">SP</span><span><strong>StudentProjects</strong><small>Management system</small></span>
      </RouterLink>
      <div class="sidebar-scroll">
        <nav v-for="group in groups" :key="group.name" :aria-label="group.name" class="nav-group">
          <p class="nav-title">{{ group.name }}</p>
          <RouterLink v-for="item in group.items" :key="item.id" :to="item.path" class="nav-link">{{ item.title }}</RouterLink>
        </nav>
      </div>
      <div class="sidebar-footer"><span class="version-tag">SKELETON v0.1</span><p>UI preview · Chưa triển khai nghiệp vụ</p></div>
    </aside>
    <div class="main-column">
      <header class="topbar"><div><div class="eyebrow">HỆ THỐNG QUẢN LÝ DỰ ÁN SINH VIÊN</div><h1>{{ route.meta.title || 'Dashboard' }}</h1></div>
        <label class="preview-role">Xem thử vai trò <select :value="store.role" @change="onRoleChange"><option value="Student">Sinh viên</option><option value="Lecturer">Giảng viên</option><option value="Admin">Admin</option></select></label>
      </header>
      <main class="main-content"><div class="notice"><strong>Chế độ xem skeleton.</strong> Chuyển vai trò chỉ thay đổi menu demo, không phải đăng nhập hay phân quyền thật. Chức năng xử lý chưa được lập trình.</div><RouterView /></main>
    </div>
  </div>
</template>
