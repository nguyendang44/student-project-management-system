<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { v08Api, type DashboardV08 } from '../features/v08/v08.api'
const data = ref<DashboardV08 | null>(null)
const error = ref('')
const loading = ref(false)
let refreshTimer: ReturnType<typeof setInterval> | undefined
async function load() {
  if (loading.value) return
  loading.value = true
  try { data.value = await v08Api.dashboard(); error.value = '' }
  catch (e) { error.value = e instanceof Error ? e.message : 'Không thể tải Dashboard.' }
  finally { loading.value = false }
}
const fmt = (s: string) => new Date(s).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
const pct = (value: number | null) => value == null ? 'Chưa có dữ liệu' : value.toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + '%'
onMounted(() => { void load(); refreshTimer = setInterval(() => { void load() }, 60000) })
onUnmounted(() => { if (refreshTimer) clearInterval(refreshTimer) })
</script>
<template>
  <section class="v08-panel"><header class="v08-head"><div><h2>Công việc cần xử lý</h2><p>Thống kê các Project được phép truy cập.</p></div><button type="button" :disabled="loading" @click="load">Làm mới</button></header>
  <p v-if="error" role="alert">{{ error }}</p>
  <template v-if="data">
    <div class="v08-cards">
      <div><strong>{{ data.projectCount }}</strong><span>Project</span></div>
      <div><strong>{{ data.pendingPlans }}</strong><span>Kế hoạch chờ duyệt</span></div>
      <div><strong>{{ data.pendingReviews }}</strong><span>Kết quả chờ đánh giá</span></div>
      <div><strong>{{ data.revisions }}</strong><span>Cần chỉnh sửa</span></div>
      <div><strong>{{ data.overdue }}</strong><span>Đồ án quá hạn chưa hoàn thành</span></div>
      <div><strong>{{ data.atRisk }}</strong><span>Đồ án có nguy cơ trễ hạn</span></div>
      <div><strong>{{ data.pendingGuidance }}</strong><span>Nhật ký chưa xác nhận</span></div>
    </div>
    <h3>Lịch và cảnh báo tiến độ toàn dự án</h3>
    <p class="deadline-note">So % tiến độ được tính từ bằng chứng/đánh giá với % thời gian kế hoạch đã trôi qua. Cảnh báo quá hạn là chưa đủ tiến độ được tính, chưa phải kết luận về thời điểm nộp tệp.</p>
    <p v-if="!data.schedules.length">Chưa có đồ án đang hoạt động.</p>
    <article v-for="project in data.schedules" :key="project.id" class="project-schedule" :class="project.warning ? 'schedule-' + project.warning.level : ''">
      <strong>{{ project.title }}</strong>
      <dl>
        <div><dt>Ngày bắt đầu</dt><dd>{{ project.startAt ? fmt(project.startAt) : 'Chưa đặt' }}</dd></div>
        <div><dt>Ngày kết thúc</dt><dd>{{ project.deadlineAt ? fmt(project.deadlineAt) : 'Chưa đặt' }}</dd></div>
        <div><dt>Tiến độ toàn dự án</dt><dd>{{ pct(project.percent) }}</dd></div>
      </dl>
      <p v-if="project.warning" role="status"><strong>{{ project.warning.code === 'OVERDUE' ? 'Quá hạn chưa hoàn thành' : project.warning.level === 'info' ? 'Cần bổ sung dữ liệu' : 'Nguy cơ trễ hạn' }}</strong> · {{ project.warning.message }}</p>
      <p v-else>{{ project.percent === 100 ? 'Đã đạt đủ tiến độ được tính.' : 'Chưa có cảnh báo trễ hạn theo số liệu hiện có.' }}</p>
      <RouterLink :to="`/progress?projectId=${project.projectId}`">Xem tiến độ và đầu việc</RouterLink>
      · <RouterLink :to="`/milestones?projectId=${project.projectId}`">Xem hoặc đặt lịch dự án</RouterLink>
    </article>
    <h3>Deadline trong 7 ngày tới</h3>
    <p v-if="!data.upcoming.length">Không có đồ án sắp đến hạn.</p>
    <ul v-else><li v-for="m in data.upcoming" :key="m.id"><strong>{{ m.title }}</strong> · Kết thúc {{ fmt(m.deadlineAt) }} <RouterLink :to="`/milestones?projectId=${m.projectId}`">Xem đồ án</RouterLink></li></ul>
    <p><RouterLink to="/notifications">Xem thông báo</RouterLink> · <RouterLink to="/guidance">Nhật ký hướng dẫn</RouterLink> · <RouterLink to="/git-progress">Phân tích tiến độ Git</RouterLink></p>
  </template></section>
</template>
<style scoped>
.v08-panel {border:1px solid #dde3ee;border-radius:12px;padding:20px;background:#fff}
.v08-head{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap}
.v08-head p {color:#64748b}.v08-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px;margin:20px 0}
.v08-cards>div{border:1px solid #e2e8f0;border-radius:8px;padding:16px;display:grid;gap:5px}.v08-cards strong{font-size:26px}.v08-cards span{color:#475569}
li{padding:5px 0}li a{margin-left:10px}
.deadline-note { color: #64748b; line-height: 1.5; }
.project-schedule { border: 1px solid #cbd5e1; border-radius: 8px; padding: 14px; margin: 12px 0; }
.project-schedule dl { display: flex; flex-wrap: wrap; gap: 12px 28px; }
.project-schedule dt { color: #475569; font-size: .85rem; }.project-schedule dd { margin: 4px 0 0; font-weight: 600; }
.schedule-warning { border-color: #fbbf24; background: #fffbeb; }
.schedule-danger { border-color: #fca5a5; background: #fef2f2; }
.schedule-info { border-color: #93c5fd; background: #eff6ff; }
</style>
