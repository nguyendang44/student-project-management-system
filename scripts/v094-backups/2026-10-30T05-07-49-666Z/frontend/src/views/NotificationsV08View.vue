<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router' // V0804_NOTIFICATION_LINKS
import { v08Api, type NotificationV08 } from '../features/v08/v08.api'
import { formatDateTimeV08 } from '../features/v08/v08DateTime' // V0802_AMPM_JOURNAL
const router = useRouter()
const openingId = ref('')
const items = ref<NotificationV08[]>([])
const unread = ref(0)
const loading = ref(false)
const error = ref('')
const fmt = formatDateTimeV08
async function load() {
  loading.value = true; error.value = ''
  try { const data = await v08Api.notifications(); items.value = data.items; unread.value = data.unreadCount }
  catch (e) { error.value = e instanceof Error ? e.message : 'Không thể tải thông báo.' }
  finally { loading.value = false }
}
async function read(item: NotificationV08) {
  if (item.isRead) return
  try { await v08Api.markRead(item.id); await load() }
  catch (e) { error.value = e instanceof Error ? e.message : 'Không thể đánh dấu đã đọc.' }
}
function canOpen(item: NotificationV08): boolean {
  // Do not navigate to external URLs, even if a response is unexpectedly malformed.
  return typeof item.targetUrl === 'string' && /^\/(milestones|progress|submissions|evaluations|guidance)\?/.test(item.targetUrl)
}
async function openNotification(item: NotificationV08) {
  if (!canOpen(item) || openingId.value) return
  openingId.value = item.id
  error.value = ''
  try {
    if (!item.isRead) {
      await v08Api.markRead(item.id)
      item.isRead = true
      unread.value = Math.max(0, unread.value - 1)
    }
    await router.push(item.targetUrl!)
  } catch (e) { error.value = e instanceof Error ? e.message : 'Không thể mở thông báo.' }
  finally { openingId.value = '' }
}
async function readAll() {
  try { await v08Api.markAllRead(); await load() }
  catch (e) { error.value = e instanceof Error ? e.message : 'Không thể cập nhật thông báo.' }
}
onMounted(load)
</script>
<template>
  <section class="v08-panel">
    <header class="v08-heading"><div><h2>Thông báo</h2><p>Thông báo được lưu trong hệ thống, chỉ người nhận mới xem được.</p></div>
      <div class="v08-actions"><button type="button" @click="load" :disabled="loading">Làm mới</button>
      <button type="button" @click="readAll" :disabled="!unread">Đánh dấu tất cả đã đọc</button></div></header>
    <p v-if="unread"><strong>{{ unread }}</strong> thông báo chưa đọc</p>
    <p v-if="error" role="alert" class="v08-error">{{ error }}</p>
    <p v-if="!loading && !items.length">Chưa có thông báo.</p>
    <ul class="v08-list">
      <li v-for="item in items" :key="item.id" :class="{ 'v08-unread': !item.isRead }">
        <div class="v08-notice-body">
          <button v-if="canOpen(item)" type="button" class="v08-notice-link" :disabled="!!openingId"
            :aria-label="'Mở nội dung thông báo: ' + item.message" @click="openNotification(item)">
            <strong>{{ item.message }}</strong><small>{{ fmt(item.createdAt) }}</small>
            <span class="v08-open-hint">Mở nội dung liên quan →</span>
          </button>
          <template v-else><p>{{ item.message }}</p><small>{{ fmt(item.createdAt) }}</small></template>
        </div>
        <button type="button" v-if="!item.isRead" @click="read(item)">Đã đọc</button>
      </li>
    </ul>
  </section>
</template>
<style scoped>
.v08-panel { background:var(--surface,#fff);border:1px solid #dce2eb;border-radius:12px;padding:20px; }
.v08-heading,.v08-actions,.v08-list li {display:flex;align-items:center;justify-content:space-between;gap:12px;}
.v08-heading {flex-wrap:wrap}.v08-heading p {color:#64748b}
.v08-actions {justify-content:flex-start; flex-wrap:wrap;}
.v08-list {list-style:none;padding:0}.v08-list li {border-top:1px solid #e5e7eb;padding:12px;align-items:flex-start;}
.v08-list li p {margin:0 0 5px}.v08-list small {color:#64748b}.v08-unread {background:#eff6ff;padding:12px;border-radius:6px;}
.v08-error {color:#b91c1c}button{cursor:pointer}
/* V0804_NOTIFICATION_LINKS */
.v08-notice-body{flex:1;min-width:0}
.v08-notice-link{display:grid;gap:4px;width:100%;text-align:left;padding:2px 0;border:0;background:transparent;color:inherit;white-space:normal;cursor:pointer}
.v08-notice-link:hover strong{text-decoration:underline}
.v08-notice-link:focus-visible{outline:2px solid #2563eb;outline-offset:3px}
.v08-notice-link strong{font-weight:500}
.v08-notice-link small{font-size:.83rem}
.v08-open-hint{color:#2563eb;font-size:.82rem}
</style>
