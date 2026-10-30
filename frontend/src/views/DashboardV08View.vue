<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { v08Api, type DashboardV08 } from '../features/v08/v08.api'
const data = ref<DashboardV08 | null>(null)
const error = ref('')
async function load() {
  try { data.value = await v08Api.dashboard(); error.value = '' }
  catch (e) { error.value = e instanceof Error ? e.message : 'Không thể tải Dashboard.' }
}
const fmt = (s: string) => new Date(s).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
onMounted(load)
</script>
<template>
  <section class="v08-panel"><header class="v08-head"><div><h2>Công việc cần xử lý</h2><p>Thống kê các Project được phép truy cập.</p></div><button type="button" @click="load">Làm mới</button></header>
  <p v-if="error" role="alert">{{ error }}</p>
  <template v-if="data">
    <div class="v08-cards">
      <div><strong>{{ data.projectCount }}</strong><span>Project</span></div>
      <div><strong>{{ data.pendingPlans }}</strong><span>Kế hoạch chờ duyệt</span></div>
      <div><strong>{{ data.pendingReviews }}</strong><span>Kết quả chờ đánh giá</span></div>
      <div><strong>{{ data.revisions }}</strong><span>Cần chỉnh sửa</span></div>
      <div><strong>{{ data.overdue }}</strong><span>Milestone quá hạn</span></div>
      <div><strong>{{ data.pendingGuidance }}</strong><span>Nhật ký chưa xác nhận</span></div>
    </div>
    <h3>Deadline trong 7 ngày tới</h3>
    <p v-if="!data.upcoming.length">Không có Milestone sắp đến hạn.</p>
    <ul v-else><li v-for="m in data.upcoming" :key="m.id"><strong>{{ m.title }}</strong> · {{ fmt(m.deadlineAt) }} <RouterLink :to="`/milestones?projectId=${m.projectId}`">Xem Milestone</RouterLink></li></ul>
    <p><RouterLink to="/notifications">Xem thông báo</RouterLink> · <RouterLink to="/guidance">Nhật ký hướng dẫn</RouterLink></p>
  </template></section>
</template>
<style scoped>
.v08-panel {border:1px solid #dde3ee;border-radius:12px;padding:20px;background:#fff}
.v08-head{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap}
.v08-head p {color:#64748b}.v08-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px;margin:20px 0}
.v08-cards>div{border:1px solid #e2e8f0;border-radius:8px;padding:16px;display:grid;gap:5px}.v08-cards strong{font-size:26px}.v08-cards span{color:#475569}
li{padding:5px 0}li a{margin-left:10px}
</style>
