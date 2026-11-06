<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HttpError } from '../api/client'
import { useAuthStore } from '../stores/auth'
import { projectProgressApi, type ProjectV07 } from '../features/progress/projectProgress.api'
import { githubApi } from '../features/github/github.api'
import type { GithubRepositoryLink } from '../features/github/github.types'
import {
  codeAnalysisApi,
  type AnalysisRunListItem,
  type AnalysisRun,
  type AnalysisFinding,
  type AnalysisCompare,
} from '../features/codeAnalysis/codeAnalysis.api'

const auth = useAuthStore()
const route = useRoute()
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const links = ref<GithubRepositoryLink[]>([])
const repositoryId = ref('')
const branches = ref<string[]>([])
const branch = ref('')
const runs = ref<AnalysisRunListItem[]>([])
const active = ref<AnalysisRun | null>(null)
const older = ref('')
const newer = ref('')
const comparison = ref<AnalysisCompare | null>(null)
const reviewComment = ref('')
const busy = ref(false)
const loading = ref(false)
const error = ref('')
const info = ref('')
let refreshTimer: ReturnType<typeof setTimeout> | undefined

const selectedRepository = computed(() => links.value.find(l => l.id === repositoryId.value))
const canStart = computed(() => auth.role === 'Student' || auth.role === 'Lecturer')
const canReview = computed(() => auth.role === 'Lecturer' && active.value?.status === 'COMPLETED')
const completedRuns = computed(() => runs.value.filter(r => r.status === 'COMPLETED'))
const analyzedPaths = computed<string[]>(() => {
  try {
    const parsed: unknown = JSON.parse(active.value?.analyzedPathsJson || '[]')
    return Array.isArray(parsed) ? parsed.filter((p): p is string => typeof p === 'string') : []
  } catch { return [] }
})

const analyzedPathText = computed(() => analyzedPaths.value.map((path, index) => `${index + 1}. ${path}`).join('\n'))

function failure(e: unknown): string {
  if (e instanceof HttpError) return e.payload?.message || `API lỗi ${e.status}`
  return e instanceof Error ? e.message : 'Có lỗi xảy ra.'
}
function fmt(date: string | null | undefined): string {
  return date ? new Date(date).toLocaleString('vi-VN') : 'Chưa có'
}
function sourceUrl(f: AnalysisFinding): string | undefined {
  const base = active.value?.repositoryUrl
  const sha = active.value?.commitSha
  if (!base || !sha || !/^[0-9a-f]{40}$/i.test(sha)) return undefined
  try {
    const url = new URL(base)
    if (url.protocol !== 'https:' || url.hostname !== 'github.com' || url.port || url.username || url.password) return undefined
    if (url.pathname.split('/').filter(Boolean).length !== 2) return undefined
    return `${url.origin}${url.pathname.replace(/\/$/, '')}/blob/${sha}/${f.filePath.split('/').map(encodeURIComponent).join('/')}#L${f.lineNumber}`
  } catch { return undefined }
}
function stopPolling() {
  if (refreshTimer !== undefined) clearTimeout(refreshTimer)
  refreshTimer = undefined
}
function schedulePolling() {
  stopPolling()
  if (!active.value || !['PENDING', 'RUNNING'].includes(active.value.status)) return
  const id = active.value.id
  refreshTimer = setTimeout(async () => {
    try {
      if (active.value?.id === id) {
        active.value = await codeAnalysisApi.get(id)
        if (repositoryId.value) runs.value = await codeAnalysisApi.list(repositoryId.value)
      }
    } catch (e) { error.value = failure(e) }
    schedulePolling()
  }, 3500)
}
async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    projects.value = await projectProgressApi.projects()
    const requested = typeof route.query.projectId === 'string' ? route.query.projectId : ''
    projectId.value = projects.value.find(p => p.id === requested)?.id || projects.value[0]?.id || ''
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function loadRepos() {
  stopPolling()
  links.value = []; repositoryId.value = ''; active.value = null; runs.value = []; comparison.value = null
  if (!projectId.value) return
  loading.value = true; error.value = ''
  try {
    links.value = await githubApi.list(projectId.value)
    repositoryId.value = links.value[0]?.id || ''
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function loadRuns() {
  stopPolling(); active.value = null; runs.value = []; branches.value = []; comparison.value = null
  const link = selectedRepository.value
  if (!link) return
  branch.value = link.defaultBranch || ''
  loading.value = true; error.value = ''
  try {
    // History is in SQL Server. GitHub failure must not hide existing reports.
    const history = await codeAnalysisApi.list(link.id)
    runs.value = history
    branches.value = branch.value ? [branch.value] : []
    if (history[0]) await viewRun(history[0].id)
    try {
      const b = await githubApi.branches(link.id)
      if (b.items.length) branches.value = b.items
    } catch (_branchError) {
      info.value = 'Không tải được danh sách branch từ GitHub. Lịch sử phân tích vẫn xem được; thử tải lại branch sau.'
    }
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function viewRun(id: string) {
  stopPolling(); error.value = ''; comparison.value = null
  try {
    active.value = await codeAnalysisApi.get(id)
    schedulePolling()
  } catch (e) { error.value = failure(e) }
}
async function start() {
  if (!selectedRepository.value || !branch.value || !canStart.value || busy.value) return
  busy.value = true; error.value = ''; info.value = ''
  try {
    const result = await codeAnalysisApi.start(repositoryId.value, branch.value)
    runs.value = await codeAnalysisApi.list(repositoryId.value)
    await viewRun(result.id)
    info.value = result.cached ? 'Đã mở báo cáo có sẵn cho commit này.' : 'Yêu cầu đã được ghi nhận, hệ thống đang xử lý.'
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
async function doCompare() {
  if (!repositoryId.value || !older.value || !newer.value) return
  error.value = ''
  try { comparison.value = await codeAnalysisApi.compare(repositoryId.value, older.value, newer.value) }
  catch (e) { error.value = failure(e) }
}
async function submitReview() {
  if (!active.value || !canReview.value || busy.value) return
  busy.value = true; error.value = ''
  try {
    await codeAnalysisApi.review(active.value.id, reviewComment.value.trim())
    reviewComment.value = ''
    active.value = await codeAnalysisApi.get(active.value.id)
    info.value = 'Đã lưu nhận xét giảng viên.'
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
watch(projectId, () => { void loadRepos() })
watch(repositoryId, () => { void loadRuns() })
onMounted(() => { void loadProjects() })
onUnmounted(stopPolling)
</script>

<template>
  <section class="analysis-v11">
    <header>
      <small>GitHub · v0.11</small>
      <h2>AI Code Analysis</h2>
      <p><RouterLink to="/git-progress">Phân tích tiến độ Git giữa hai commit →</RouterLink></p>
      <p>Phân tích tham khảo theo commit SHA. Nhận xét AI không phải điểm số hay kết luận cuối cùng.</p>
    </header>

    <p v-if="error" class="alert error" role="alert">{{ error }}</p>
    <p v-if="info" class="alert" role="status">{{ info }}</p>

    <div class="controls">
      <label>Project
        <select v-model="projectId" :disabled="loading">
          <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.id }}</option>
        </select>
      </label>
      <label>GitHub repository
        <select v-model="repositoryId" :disabled="loading || !links.length">
          <option v-for="r in links" :key="r.id" :value="r.id">{{ r.repositoryUrl }}</option>
        </select>
      </label>
      <label>Branch
        <select v-model="branch" :disabled="loading || !branches.length">
          <option v-for="b in branches" :key="b" :value="b">{{ b }}</option>
        </select>
      </label>
      <button :disabled="!canStart || !repositoryId || !branch || busy || loading" @click="start">
        {{ busy ? 'Đang gửi...' : 'Phân tích mã nguồn' }}
      </button>
      <button class="secondary" :disabled="!repositoryId || loading" @click="loadRuns">Làm mới</button>
    </div>

    <p v-if="!links.length && !loading">Project này chưa có repository. Hãy liên kết trong trang GitHub repositories.</p>
    <div v-if="repositoryId" class="columns">
      <section class="panel">
        <h3>Lịch sử phân tích</h3>
        <p v-if="!runs.length">Chưa có lần phân tích nào.</p>
        <button v-for="run in runs" :key="run.id" class="run" :class="{ selected: active?.id === run.id }"
          @click="viewRun(run.id)">
          <b>{{ run.commitSha.slice(0, 10) }}</b> · {{ run.branch }} · {{ run.scope || 'sampled' }}
          <span>{{ run.status }} · {{ fmt(run.createdAt) }}</span>
        </button>
      </section>

      <section class="panel report">
        <template v-if="active">
          <h3>Báo cáo · {{ active.commitSha.slice(0, 12) }}</h3>
          <p class="meta">Phạm vi: {{ active.scope || 'sampled' }}</p>
          <p>Trạng thái: <strong>{{ active.status }}</strong></p>
          <p v-if="active.status === 'PENDING' || active.status === 'RUNNING'">Đang xử lý ở backend. Giao diện tự kiểm tra kết quả.</p>
          <p v-if="active.status === 'FAILED'" class="alert error">{{ active.errorMessage }}</p>
          <template v-if="active.status === 'COMPLETED'">
            <p class="meta">Mô hình: {{ active.modelName }} · {{ fmt(active.completedAt) }} · Đã gửi {{ analyzedPaths.length }} file · Chưa phân tích / bỏ qua {{ active.skippedFiles }} file</p>
            <p class="meta">Phân tích theo các đoạn mã được chọn; không có nghĩa đã đọc toàn bộ nội dung file hoặc repository.</p>
            <details v-if="analyzedPaths.length">
              <summary>Danh sách file đã đưa vào AI ({{ analyzedPaths.length }})</summary>
              <pre class="source-paths-plain">{{ analyzedPathText }}</pre>
            </details>
            <p class="meta">Báo cáo chỉ dựa trên đoạn mã đã gửi; nhận xét về lỗi/bảo mật phải được đối chiếu với ngữ cảnh và hành vi thực tế.</p>
            <h4>Tổng quan</h4>
            <p>{{ active.summary || 'Không có nhận xét.' }}</p>
            <h4>Những phần đang phát triển</h4>
            <p>{{ active.developmentWork || 'Chưa đủ bằng chứng.' }}</p>
            <h4>Phát hiện ({{ active.findings.length }})</h4>
            <p v-if="!active.findings.length">Không có phát hiện vượt qua bộ lọc bằng chứng trong phạm vi file đã đọc; không có nghĩa repository an toàn.</p>
            <article v-for="finding in active.findings" :key="finding.id" class="finding">
              <strong>{{ finding.severity }} · {{ finding.category }}</strong>
              <p class="meta"><b>{{ finding.verificationStatus === 'NEEDS_REVIEW' ? 'CẦN GIẢNG VIÊN XÁC MINH' : 'NHẬN XÉT AI CHƯA XÁC MINH' }}</b> · Mức độ chỉ là ước lượng của AI, không phải kết luận bảo mật.</p>
              <p><a v-if="sourceUrl(finding)" :href="sourceUrl(finding)" target="_blank" rel="noopener noreferrer">
                {{ finding.filePath }}:{{ finding.lineNumber }}
              </a><span v-else>{{ finding.filePath }}:{{ finding.lineNumber }}</span></p>
              <pre>{{ finding.evidence }}</pre>
              <p>{{ finding.explanation }}</p>
              <p><b>Đề xuất:</b> {{ finding.recommendation }}</p>
            </article>
            <h4>Nhận xét giảng viên</h4>
            <article v-for="review in active.reviews" :key="review.id" class="review">
              <small>{{ fmt(review.createdAt) }}</small><p>{{ review.comment }}</p>
            </article>
            <div v-if="canReview" class="review-form">
              <textarea v-model="reviewComment" rows="3" maxlength="2000" placeholder="Nhận xét có căn cứ, công việc sinh viên cần cải thiện..."></textarea>
              <button :disabled="busy || reviewComment.trim().length < 5" @click="submitReview">Gửi phản hồi</button>
            </div>
          </template>
        </template>
        <p v-else>Chọn một lần phân tích ở bên trái.</p>
      </section>
    </div>

    <section v-if="completedRuns.length >= 2" class="panel compare">
      <h3>So sánh hai lần phân tích</h3>
      <div class="controls">
        <label>Bản cũ
          <select v-model="older"><option value="">Chọn</option><option v-for="r in completedRuns" :key="r.id" :value="r.id">{{ r.commitSha.slice(0, 8) }}</option></select>
        </label>
        <label>Bản mới
          <select v-model="newer"><option value="">Chọn</option><option v-for="r in completedRuns" :key="r.id" :value="r.id">{{ r.commitSha.slice(0, 8) }}</option></select>
        </label>
        <button :disabled="!older || !newer || older === newer" @click="doCompare">So sánh</button>
      </div>
      <template v-if="comparison">
        <p class="meta">{{ comparison.note }}</p>
        <div class="compare-grid">
          <div><strong>{{ comparison.sameCommit ? 'Chỉ có ở báo cáo cũ' : 'Có thể đã xử lý' }}: {{ comparison.resolvedCandidates.length }}</strong>
            <p v-for="f in comparison.resolvedCandidates" :key="f.id">{{ f.filePath }}:{{ f.lineNumber }} · {{ f.explanation }}</p></div>
          <div><strong>Vẫn xuất hiện: {{ comparison.persistent.length }}</strong>
            <p v-for="f in comparison.persistent" :key="f.id">{{ f.filePath }}:{{ f.lineNumber }} · {{ f.explanation }}</p></div>
          <div><strong>{{ comparison.sameCommit ? 'Chỉ có ở báo cáo mới' : 'Phát hiện mới' }}: {{ comparison.newFindings.length }}</strong>
            <p v-for="f in comparison.newFindings" :key="f.id">{{ f.filePath }}:{{ f.lineNumber }} · {{ f.explanation }}</p></div>
        </div>
      </template>
    </section>
  </section>
</template>

<style scoped>
.source-paths-plain { white-space:pre-wrap; overflow-wrap:anywhere; font: .85rem/1.65 ui-monospace, SFMono-Regular, Consolas, monospace; background:rgba(100,116,139,.08); padding:.75rem; border-radius:6px; }
.analysis-v11 { display: grid; gap: 1rem; max-width: 1200px; margin: 0 auto; }
.analysis-v11 header small { color: var(--text-muted, #64748b); }
.analysis-v11 h2 { margin: .2rem 0; }
.analysis-v11 h3 { margin-top: 0; }
.controls { display:flex; flex-wrap:wrap; align-items:end; gap:.7rem; }
.controls label { display:grid; gap:.35rem; min-width:180px; flex:1; }
.controls select,textarea { width:100%; min-height:38px; padding:.5rem; border:1px solid #cbd5e1; border-radius:6px; background:var(--surface,#fff); color:inherit; }
button { cursor:pointer; padding:.6rem .8rem; background:#1d4ed8; border:0; color:#fff; border-radius:6px; }
button.secondary { background:#475569; }
button:disabled { opacity:.55; cursor:default; }
.columns { display:grid; grid-template-columns:minmax(240px,1fr) minmax(0,3fr); gap:1rem; }
.panel { border:1px solid #cbd5e1; border-radius:10px; padding:1rem; min-width:0; }
.run { width:100%; text-align:left; display:grid; gap:.3rem; background:transparent; color:inherit; border:1px solid #cbd5e1; margin-bottom:.5rem; }
.run.selected { border-color:#2563eb; background:rgba(37,99,235,.08); }
.run span,.meta { color:var(--text-muted,#64748b); font-size:.875rem; }
.finding { border-top:1px solid #cbd5e1; padding:.75rem 0; overflow-wrap:anywhere; }
.finding pre { white-space:pre-wrap; word-break:break-word; background:rgba(100,116,139,.1); padding:.75rem; border-radius:6px; }
.source-paths { padding-left: 1.25rem; }
.source-path { display:inline-block; overflow-wrap:anywhere; font-family:ui-monospace, SFMono-Regular, Consolas, monospace; font-size:.85rem; }
.review { border-left:3px solid #64748b; padding:.25rem .75rem; margin:.5rem 0; }
.review-form { display:grid; gap:.5rem; }
.review-form button { justify-self:start; }
.alert { padding:.7rem; border:1px solid #94a3b8; border-radius:6px; }
.alert.error { border-color:#dc2626; color:#b91c1c; }
.compare-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:1rem; }
.compare-grid > div { background:rgba(100,116,139,.08); padding:1rem; border-radius:8px; }
@media(max-width:800px) { .columns,.compare-grid { grid-template-columns:1fr; } }
</style>
