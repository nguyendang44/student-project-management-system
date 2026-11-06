<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HttpError } from '../api/client'
import { useAuthStore } from '../stores/auth'
import { projectProgressApi, type ProjectV07 } from '../features/progress/projectProgress.api'
import { githubApi } from '../features/github/github.api'
import type { GithubRepositoryLink, GithubCommit } from '../features/github/github.types'
import { gitProgressApi, type GitProgressRun, type GitProgressRunListItem,
  type GitProgressFile, type GitProgressEvidence } from '../features/gitProgress/gitProgress.api'

const route = useRoute()
const auth = useAuthStore()
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const repos = ref<GithubRepositoryLink[]>([])
const repositoryId = ref('')
const commits = ref<GithubCommit[]>([])
const baseSha = ref('')
const headSha = ref('')
const runs = ref<GitProgressRunListItem[]>([])
const active = ref<GitProgressRun | null>(null)
const busy = ref(false)
const loading = ref(false)
const error = ref('')
const info = ref('')
let timer: ReturnType<typeof setTimeout> | undefined
const canRun = computed(() => auth.role === 'Student' || auth.role === 'Lecturer')
const shaOkay = (sha: string) => /^[0-9a-f]{40}$/i.test(sha)
const ready = computed(() => !!repositoryId.value && canRun.value && shaOkay(baseSha.value) &&
  shaOkay(headSha.value) && baseSha.value.toLowerCase() !== headSha.value.toLowerCase())
const selected = computed(() => repos.value.find(r => r.id === repositoryId.value))
const files = computed<GitProgressFile[]>(() => parseArray<GitProgressFile>(active.value?.filesJson))
const evidence = computed<GitProgressEvidence[]>(() => parseArray<GitProgressEvidence>(active.value?.evidenceJson))
function parseArray<T>(raw?: string): T[] {
  try { const value: unknown = JSON.parse(raw || '[]'); return Array.isArray(value) ? value as T[] : [] }
  catch { return [] }
}
function failure(e: unknown): string {
  if (e instanceof HttpError) return e.payload?.message || `API lỗi ${e.status}`
  return e instanceof Error ? e.message : 'Đã xảy ra lỗi.'
}
function fmt(date: string | null | undefined): string { return date ? new Date(date).toLocaleString('vi-VN') : 'Chưa có' }
function compareLink(): string | undefined {
  if (!active.value || !shaOkay(active.value.baseSha) || !shaOkay(active.value.headSha)) return undefined
  try {
    const url = new URL(active.value.repositoryUrl || '')
    if (url.protocol !== 'https:' || url.hostname !== 'github.com' || url.port || url.username || url.password ||
        url.pathname.split('/').filter(Boolean).length !== 2) return undefined
    return `${url.origin}${url.pathname.replace(/\/$/, '')}/compare/${active.value.baseSha}...${active.value.headSha}`
  } catch { return undefined }
}
function stopPolling() { if (timer) clearTimeout(timer); timer = undefined }
function poll() {
  stopPolling()
  if (!active.value || !['PENDING', 'RUNNING'].includes(active.value.status)) return
  const id = active.value.id
  timer = setTimeout(async () => {
    try {
      if (active.value?.id === id) {
        active.value = await gitProgressApi.get(id)
        if (repositoryId.value) runs.value = await gitProgressApi.list(repositoryId.value)
      }
    } catch (e) { error.value = failure(e) }
    poll()
  }, 4000)
}
async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    projects.value = await projectProgressApi.projects()
    const wanted = typeof route.query.projectId === 'string' ? route.query.projectId : ''
    projectId.value = projects.value.find(p => p.id === wanted)?.id || projects.value[0]?.id || ''
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function loadRepos() {
  stopPolling(); repos.value = []; repositoryId.value = ''; active.value = null; runs.value = []
  if (!projectId.value) return
  loading.value = true; error.value = ''
  try {
    repos.value = await githubApi.list(projectId.value)
    repositoryId.value = repos.value[0]?.id || ''
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function loadHistory() {
  stopPolling(); runs.value = []; active.value = null; commits.value = []; baseSha.value = ''; headSha.value = ''
  if (!repositoryId.value) return
  loading.value = true; error.value = ''; info.value = ''
  try {
    // SQL history should remain visible if GitHub API is rate-limited.
    runs.value = await gitProgressApi.list(repositoryId.value)
    if (runs.value[0]) await show(runs.value[0].id)
    try {
      commits.value = (await githubApi.commits(repositoryId.value)).items
      headSha.value = commits.value[0]?.sha || ''
      baseSha.value = commits.value[1]?.sha || ''
    } catch (_e) { info.value = 'Không đọc được commit GitHub. Bạn vẫn có thể nhập hai SHA thủ công.' }
  } catch (e) { error.value = failure(e) }
  finally { loading.value = false }
}
async function show(id: string) {
  stopPolling(); error.value = ''
  try { active.value = await gitProgressApi.get(id); poll() }
  catch (e) { error.value = failure(e) }
}
async function start() {
  if (!ready.value || busy.value) return
  busy.value = true; error.value = ''; info.value = ''
  try {
    const created = await gitProgressApi.start(repositoryId.value, baseSha.value.trim(), headSha.value.trim())
    runs.value = await gitProgressApi.list(repositoryId.value)
    await show(created.id)
    info.value = created.cached ? 'Đã mở báo cáo đã lưu.' : 'Đã ghi nhận yêu cầu; backend đang so sánh Git diff và gọi Ollama.'
  } catch (e) { error.value = failure(e) }
  finally { busy.value = false }
}
watch(projectId, () => { void loadRepos() })
watch(repositoryId, () => { void loadHistory() })
onMounted(() => { void loadProjects() })
onUnmounted(stopPolling)
</script>

<template>
  <section class="git-progress-v11">
    <header>
      <small>GitHub · v0.11 · Git Progress Analysis</small>
      <h2>Phân tích tiến độ giữa hai commit</h2>
      <p>So sánh Git diff, lưu báo cáo và chỉ hiển thị nhận xét AI có dòng thay đổi làm bằng chứng.</p>
      <RouterLink to="/ai-analysis">Quay lại AI Code Analysis</RouterLink>
    </header>
    <p v-if="error" class="alert err" role="alert">{{ error }}</p>
    <p v-if="info" class="alert" role="status">{{ info }}</p>
    <div class="gp-controls panel">
      <label>Project
        <select v-model="projectId" :disabled="loading">
          <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.id }}</option>
        </select>
      </label>
      <label>Repository
        <select v-model="repositoryId" :disabled="loading">
          <option v-for="r in repos" :key="r.id" :value="r.id">{{ r.repositoryUrl }}</option>
        </select>
      </label>
      <label>Commit cũ (base SHA)
        <input v-model.trim="baseSha" list="gp-commits" placeholder="40 ký tự SHA" autocomplete="off" />
      </label>
      <label>Commit mới (head SHA)
        <input v-model.trim="headSha" list="gp-commits" placeholder="40 ký tự SHA" autocomplete="off" />
      </label>
      <datalist id="gp-commits">
        <option v-for="c in commits" :key="c.sha" :value="c.sha">{{ c.message.slice(0, 70) }}</option>
      </datalist>
      <button :disabled="!ready || busy || loading" @click="start">{{ busy ? 'Đang gửi...' : 'Phân tích tiến độ' }}</button>
      <button class="secondary" :disabled="loading || !repositoryId" @click="loadHistory">Làm mới</button>
    </div>
    <p class="hint">Mặc định chọn hai commit gần nhất từ GitHub. Nếu cần so sánh mốc cũ hơn, nhập đủ SHA 40 ký tự. Commit mới phải đi sau commit cũ trên cùng lịch sử.</p>
    <div v-if="repositoryId" class="gp-columns">
      <section class="panel history">
        <h3>Lịch sử báo cáo đã lưu</h3>
        <p v-if="!runs.length">Chưa có báo cáo.</p>
        <button v-for="run in runs" :key="run.id" class="history-item" :class="{ active: active?.id === run.id }" @click="show(run.id)">
          <b>{{ run.baseSha.slice(0, 9) }} → {{ run.headSha.slice(0, 9) }}</b>
          <small>{{ run.status }} · {{ fmt(run.createdAt) }}</small>
        </button>
      </section>
      <section class="panel results">
        <template v-if="active">
          <h3>Báo cáo · {{ active.baseSha.slice(0, 8) }} → {{ active.headSha.slice(0, 8) }}</h3>
          <p>Trạng thái: <strong>{{ active.status }}</strong> · {{ fmt(active.completedAt) }}</p>
          <p v-if="['PENDING', 'RUNNING'].includes(active.status)">Backend đang xử lý. Kết quả sẽ tự cập nhật.</p>
          <p v-if="active.status === 'FAILED'" class="alert err">{{ active.errorMessage }}</p>
          <template v-if="active.status === 'COMPLETED'">
            <div class="metrics">
              <div><b>{{ active.totalCommits }}</b><small>Commit mới</small></div>
              <div><b>{{ active.totalFiles }}</b><small>File thay đổi</small></div>
              <div><b>+{{ active.additions }}</b><small>Dòng thêm</small></div>
              <div><b>-{{ active.deletions }}</b><small>Dòng xóa</small></div>
            </div>
            <p>{{ active.summary }}</p>
            <p class="hint">AI {{ active.modelName }} · đã gửi đoạn diff của {{ active.sentFiles }} file.
              <span v-if="active.partialCoverage">Phạm vi GitHub trả về có thể chưa đầy đủ.</span>
            </p>
            <p v-if="compareLink()"><a :href="compareLink()" target="_blank" rel="noopener noreferrer">Xem đối chiếu gốc trên GitHub</a></p>
            <h4>Những thay đổi có bằng chứng ({{ evidence.length }})</h4>
            <p v-if="!evidence.length">Không có nhận xét AI nào vượt qua kiểm tra dòng diff. Xem các file bên dưới để tự đánh giá.</p>
            <article v-for="(item, i) in evidence" :key="i" class="evidence">
              <b>{{ item.file }}</b><p>{{ item.description }}</p><pre>{{ item.evidence }}</pre>
              <small>Nhận xét AI, cần giảng viên xác minh; không chứng minh code chạy thành công.</small>
            </article>
            <details>
              <summary>Danh sách file thay đổi ({{ files.length }})</summary>
              <div v-for="f in files" :key="f.path" class="file-line">
                <span>{{ f.status }} · {{ f.path }}</span>
                <small>+{{ f.additions }} / -{{ f.deletions }}</small>
              </div>
            </details>
          </template>
        </template>
        <p v-else>Chọn một báo cáo trong lịch sử hoặc tạo mới.</p>
      </section>
    </div>
  </section>
</template>

<style scoped>
.git-progress-v11 { max-width: 1200px; margin: 0 auto; display: grid; gap: 1rem; }
.git-progress-v11 h2 { margin: .3rem 0; }
.git-progress-v11 header small,.hint { color: var(--text-muted,#64748b); font-size: .9rem; }
.panel { padding: 1rem; border-radius: 10px; border: 1px solid #cbd5e1; }
.gp-controls { display: flex; flex-wrap: wrap; align-items: end; gap: .8rem; }
.gp-controls label { flex: 1 1 220px; display: grid; gap: .4rem; }
.gp-controls input,.gp-controls select { min-height: 38px; padding: .5rem; background: var(--surface,#fff); color: inherit; border: 1px solid #94a3b8; border-radius: 6px; width: 100%; }
button { cursor: pointer; padding: .65rem .8rem; background: #1d4ed8; border: 0; border-radius: 6px; color: white; }
button:disabled { opacity: .55; cursor: default; }
button.secondary { background: #475569; }
.gp-columns { display: grid; grid-template-columns: minmax(220px,1fr) minmax(0,2.2fr); gap: 1rem; }
@media (max-width: 800px) { .gp-columns { grid-template-columns: 1fr; } }
.history-item { text-align: left; display: grid; width: 100%; gap: .3rem; background: transparent; border: 1px solid #cbd5e1; color: inherit; margin-bottom: .45rem; }
.history-item.active { border-color: #2563eb; }
.history-item small { color: var(--text-muted,#64748b); }
.metrics { display: grid; grid-template-columns: repeat(4,minmax(0,1fr)); gap: .5rem; }
.metrics div { border-radius: 6px; padding: .7rem; background: rgba(100,116,139,.09); display: grid; gap: .2rem; }
.metrics b { font-size: 1.25rem; }
.metrics small { color: var(--text-muted,#64748b); }
.evidence { border-top: 1px solid #cbd5e1; padding-top: .8rem; margin-top: .8rem; }
.evidence pre { white-space: pre-wrap; overflow-wrap: anywhere; background: rgba(100,116,139,.09); padding: .7rem; border-radius: 6px; }
.file-line { display: flex; justify-content: space-between; flex-wrap: wrap; gap: .8rem; border-top: 1px solid #cbd5e1; padding: .4rem; }
.file-line small { color: var(--text-muted,#64748b); }
.err { color: #b91c1c; }
</style>
