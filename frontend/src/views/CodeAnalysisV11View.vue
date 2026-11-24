<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import GitProgressWordComparison from '../components/GitProgressWordComparison.vue'
import { projectProgressApi, type ProjectV07 } from '../features/progress/projectProgress.api'
import { githubApi } from '../features/github/github.api'
import type { GithubRepositoryLink } from '../features/github/github.types'
import { gitProgressApi, type GitProgressRun, type GitProgressRunListItem,
  type GitProgressEvidence } from '../features/gitProgress/gitProgress.api'

const auth = useAuthStore()
const route = useRoute()
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const repositories = ref<GithubRepositoryLink[]>([])
const repositoryId = ref('')
const reports = ref<GitProgressRunListItem[]>([])
const reportId = ref('')
const report = ref<GitProgressRun | null>(null)
const loading = ref(false)
const reportLoading = ref(false)
const reportsLoading = ref(false)
const error = ref('')
const project = computed(() => projects.value.find(p => p.id === projectId.value))
let repositoryRequest: AbortController | undefined
let reportRequest: AbortController | undefined
let selectedRequest: AbortController | undefined
let disposed = false

const features = computed<GitProgressEvidence[]>(() => {
  try {
    const items: unknown = JSON.parse(report.value?.evidenceJson || '[]')
    if (!Array.isArray(items)) return []
    return items.filter((item): item is GitProgressEvidence => item && typeof item === 'object' &&
      typeof item.title === 'string' && item.title.trim().length > 0 &&
      ['ai-feature-report', 'diff-feature-report', 'diff-work-report', 'feature-rule'].includes(item.source) &&
      Array.isArray(item.citations) && item.citations.length > 0)
  } catch { return [] }
})
const failure = (exception: unknown) => exception instanceof Error ? exception.message : 'Không tải được báo cáo Git.'

async function loadProjects() {
  loading.value = true
  try {
    projects.value = await projectProgressApi.projects()
    if (disposed) return
    projectId.value = projects.value.find(p => p.id === route.query.projectId)?.id ?? projects.value[0]?.id ?? ''
  } catch (exception) { if (!disposed) error.value = failure(exception) }
  finally { if (!disposed) loading.value = false }
}
async function loadRepositories() {
  repositoryRequest?.abort()
  const request = new AbortController()
  repositoryRequest = request
  repositories.value = []; repositoryId.value = ''; error.value = ''
  if (!projectId.value) return
  loading.value = true
  try {
    const items = await githubApi.list(projectId.value)
    if (request.signal.aborted || disposed) return
    repositories.value = items
    repositoryId.value = items.find(item => item.id === route.query.repositoryId)?.id ?? items[0]?.id ?? ''
  } catch (exception) { if (!request.signal.aborted && !disposed) error.value = failure(exception) }
  finally { if (repositoryRequest === request && !disposed) loading.value = false }
}
async function loadReports() {
  reportRequest?.abort()
  selectedRequest?.abort()
  report.value = null; reportId.value = ''; reports.value = []
  reportsLoading.value = false
  if (!repositoryId.value) return
  const request = new AbortController()
  reportRequest = request
  reportsLoading.value = true; error.value = ''
  try {
    const items = await gitProgressApi.list(repositoryId.value, request.signal)
    if (request.signal.aborted || disposed) return
    reports.value = items.filter(item => item.status === 'COMPLETED')
    const requested = route.query.repositoryId === repositoryId.value ? route.query.runId : ''
    reportId.value = reports.value.find(item => item.id === requested)?.id ?? reports.value[0]?.id ?? ''
  } catch (exception) { if (!request.signal.aborted && !disposed) error.value = failure(exception) }
  finally { if (reportRequest === request && !disposed) reportsLoading.value = false }
}
async function loadFeatures() {
  selectedRequest?.abort()
  report.value = null
  reportLoading.value = false
  if (!reportId.value) return
  const request = new AbortController()
  selectedRequest = request
  reportLoading.value = true; error.value = ''
  try {
    const item = await gitProgressApi.get(reportId.value)
    if (request.signal.aborted || disposed) return
    if (item.repositoryLinkId !== repositoryId.value || item.status !== 'COMPLETED') {
      error.value = 'Báo cáo này chưa hoàn tất hoặc không thuộc repository đã chọn.'
      return
    }
    report.value = item
  } catch (exception) { if (!request.signal.aborted && !disposed) error.value = failure(exception) }
  finally { if (selectedRequest === request && !disposed) reportLoading.value = false }
}
watch(projectId, loadRepositories)
watch(repositoryId, loadReports)
watch(reportId, loadFeatures)
onMounted(loadProjects)
onUnmounted(() => {
  disposed = true
  repositoryRequest?.abort(); reportRequest?.abort(); selectedRequest?.abort()
})
</script>

<template>
  <section class="analysis-v11">
    <header>
      <h2>AI Code Analysis</h2>
      <p><RouterLink :to="{ path: '/git-progress', query: { projectId } }">Phân tích tiến độ giữa hai commit →</RouterLink></p>
    </header>
    <p v-if="error" class="alert" role="alert">{{ error }}</p>
    <div class="controls">
      <label>Đồ án
        <select v-model="projectId" :disabled="loading">
          <option v-if="!projects.length" value="">Chưa có đồ án</option>
          <option v-for="item in projects" :key="item.id" :value="item.id">{{ item.topicTitle }} · {{ item.studentName }}</option>
        </select>
      </label>
      <label>Repository GitHub
        <select v-model="repositoryId" :disabled="loading || !repositories.length">
          <option v-if="!repositories.length" value="">Chưa có repository liên kết</option>
          <option v-for="item in repositories" :key="item.id" :value="item.id">{{ item.repositoryUrl }}</option>
        </select>
      </label>
      <label v-if="reports.length">Cặp commit đã phân tích
        <select v-model="reportId" :disabled="reportLoading || reportsLoading">
          <option v-for="item in reports" :key="item.id" :value="item.id">
            {{ item.baseSha.slice(0, 8) }} → {{ item.headSha.slice(0, 8) }} · {{ item.modelName }}
          </option>
        </select>
      </label>
    </div>
      <p v-if="reportLoading || reportsLoading || loading" role="status">Đang tải các chức năng từ báo cáo Git…</p>
      <p v-else-if="repositoryId && !reports.length">
        Repository này chưa có báo cáo Git hoàn tất.
        <RouterLink :to="{ path: '/git-progress', query: { projectId } }">Tạo báo cáo giữa hai commit</RouterLink>
      </p>
    <div class="analysis-content" :class="{ 'with-word': auth.role === 'Lecturer' }">
      <section v-if="report" class="features">
        <h3>Các chức năng có bằng chứng từ Git ({{ features.length }})</h3>
        <p class="context">{{ project?.topicTitle }} · {{ report.baseSha.slice(0, 8) }} → {{ report.headSha.slice(0, 8) }}</p>
        <p v-if="!features.length">Báo cáo đã chọn chưa có nhóm chức năng kèm trích dẫn.</p>
        <article v-for="(item, index) in features" :key="item.featureId || item.factId || index" class="feature">
          <h4>{{ item.title }}</h4>
          <p>{{ item.description }}</p>
          <details v-if="item.citations?.length">
            <summary>Xem bằng chứng Git</summary>
            <div v-for="(citation, i) in item.citations" :key="i">
              <p class="path">{{ citation.file }}</p>
              <pre>{{ citation.evidence }}</pre>
            </div>
          </details>
        </article>
      </section>
      <GitProgressWordComparison v-if="auth.role === 'Lecturer'" :report="report" />
    </div>
  </section>
</template>

<style scoped>
.analysis-v11 { display: grid; gap: 1rem; max-width: 1200px; margin: 0 auto; }
.analysis-v11 h2, .features h3 { margin: .2rem 0; }
.analysis-content { display: grid; gap: 1rem; align-items: start; }
.analysis-content.with-word { grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); }
@media (max-width: 900px) { .analysis-content.with-word { grid-template-columns: minmax(0, 1fr); } }
.controls { display: flex; flex-wrap: wrap; gap: .8rem; }
.controls label { display: grid; gap: .4rem; flex: 1 1 230px; min-width: 0; }
.controls select { width: 100%; min-height: 40px; padding: .5rem; border: 1px solid #cbd5e1; border-radius: 6px; background: var(--surface, #fff); color: inherit; }
.features { min-width: 0; }
.feature { margin-top: 1rem; padding: 1rem; border: 1px solid #cbd5e1; border-radius: 10px; }
.feature h4 { margin: 0 0 .5rem; }
.feature summary { cursor: pointer; }
.feature pre { white-space: pre-wrap; overflow-wrap: anywhere; padding: .65rem; background: rgba(100,116,139,.08); }
.path { overflow-wrap: anywhere; font-size: .85rem; }
.context { color: var(--text-muted, #64748b); }
.alert { color: #b91c1c; }
</style>
