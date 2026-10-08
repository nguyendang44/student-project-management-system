<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { lecturersApi } from '../features/lecturers/lecturers.api'
import type { LecturerDirectoryEntry } from '../features/lecturers/lecturers.types'
const items = ref<LecturerDirectoryEntry[]>([])
const loading = ref(true)
const error = ref('')
onMounted(async () => { try { items.value = await lecturersApi.list() } catch (err) { error.value = err instanceof Error ? err.message : 'Không tải được giảng viên.' } finally { loading.value = false } })
</script>
<template>
  <section class="intro"><span class="section-chip">Hướng dẫn · FR-04</span><h2>Danh sách giảng viên</h2><p>Thông tin giảng viên đang hoạt động. Capacity theo từng đợt đăng ký sẽ triển khai ở module kế tiếp.</p></section>
  <p v-if="error" role="alert" class="user-alert error">{{ error }}</p>
  <article class="panel"><div class="table-scroller"><table><thead><tr><th>Họ tên</th><th>Chuyên môn</th></tr></thead><tbody>
    <tr v-if="loading"><td colspan="2" class="empty-cell">Đang tải...</td></tr>
    <tr v-else-if="!items.length"><td colspan="2" class="empty-cell">Chưa có giảng viên đang hoạt động.</td></tr>
    <tr v-for="lecturer in items" :key="lecturer.id"><td>{{ lecturer.fullName }}</td><td>{{ lecturer.specialty || 'Chưa cập nhật' }}</td></tr>
  </tbody></table></div></article>
</template>
