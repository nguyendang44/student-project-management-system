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
  <section class="intro"><span class="section-chip">Hướng dẫn · FR-04</span><h2>Danh sách giảng viên</h2><p>Thông tin giảng viên đang hoạt động. Giới hạn hướng dẫn và số vị trí còn trống xem tại mục Sức chứa giảng viên.</p></section>
  <p v-if="error" role="alert" class="user-alert error">{{ error }}</p>
  <article class="panel"><div class="table-scroller"><table class="lecturer-directory-spaced"><thead><tr><th>Họ tên</th><th>Chuyên môn</th></tr></thead><tbody>
    <tr v-if="loading"><td colspan="2" class="empty-cell">Đang tải...</td></tr>
    <tr v-else-if="!items.length"><td colspan="2" class="empty-cell">Chưa có giảng viên đang hoạt động.</td></tr>
    <tr v-for="lecturer in items" :key="lecturer.id"><td>{{ lecturer.fullName }}</td><td>{{ lecturer.specialty || 'Chưa cập nhật' }}</td></tr>
  </tbody></table></div></article>
</template>

<style scoped>
/* v0.6.9: lecturer directory only. Distinct, breathable cells without affecting other tables. */
.lecturer-directory-spaced {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0 12px;
  table-layout: fixed;
}
.lecturer-directory-spaced th {
  padding: 14px 22px;
  background: #f4f7fc;
  color: #566783;
  border-bottom: 1px solid #e3e9f3;
}
.lecturer-directory-spaced th:first-child { width: 42%; }
.lecturer-directory-spaced tbody td {
  padding: 19px 22px;
  border-top: 1px solid #dce4f0;
  border-bottom: 1px solid #dce4f0;
  background: #fafdff;
  color: #344560;
  font-size: 14px;
  line-height: 1.6;
  vertical-align: middle;
  overflow-wrap: anywhere;
}
.lecturer-directory-spaced tbody td:first-child {
  border-left: 1px solid #dce4f0;
  border-radius: 10px 0 0 10px;
  font-weight: 650;
}
.lecturer-directory-spaced tbody td + td { border-left: 1px solid #e3e9f3; }
.lecturer-directory-spaced tbody td:last-child {
  border-right: 1px solid #dce4f0;
  border-radius: 0 10px 10px 0;
}
.lecturer-directory-spaced tbody td:only-child { border-radius: 10px; }
.lecturer-directory-spaced tbody tr:hover td { background: #f1f6ff; }
@media (max-width: 600px) {
  .lecturer-directory-spaced th { padding: 12px 14px; }
  .lecturer-directory-spaced tbody td { padding: 15px 14px; }
}
</style>
