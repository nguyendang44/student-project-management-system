<script setup lang="ts">
defineProps<{ moduleId?: string }>()
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HttpError } from '../api/client'
import { projectProgressApi, type ProjectV07 } from '../features/progress/projectProgress.api'
import { githubApi } from '../features/github/github.api'
import type { GithubCommit, GithubRepositoryLink } from '../features/github/github.types'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const route = useRoute()
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const links = ref<GithubRepositoryLink[]>([])
const activeLinkId = ref('')
const commits = ref<GithubCommit[]>([])
const branches = ref<string[]>([])
const repositoryUrl = ref('')
const loading = ref(false)
const busy = ref(false)
const loadingCommits = ref(false)
const error = ref('')
const message = ref('')
const isStudent = computed(() => auth.role === 'Student')
const activeProject = computed(() => projects.value.find(p => p.id === projectId.value))
const activeLink = computed(() => links.value.find(link => link.id === activeLinkId.value))

function errorMessage(e: unknown): string {
  if (e instanceof HttpError) return e.payload?.message ?? `API trả về lỗi ${e.status}.`
  return e instanceof Error ? e.message : 'Không thể hoàn thành thao tác.'
}
function dateTime(value: string | null): string {
  return value ? new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(new Date(value)) : 'Chưa có dữ liệu'
}
function safeGithubLink(value: string): string | undefined {
  try {
    const url = new URL(value)
    if (url.protocol !== 'https:' || url.hostname !== 'github.com' || url.port || url.username || url.password) return undefined
    return url.href
  } catch { return undefined }
}
async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    projects.value = await projectProgressApi.projects()
    const requested = typeof route.query.projectId === 'string' ? route.query.projectId : ''
    projectId.value = projects.value.find(p => p.id === requested)?.id ?? projects.value[0]?.id ?? ''
  } catch (e) { error.value = errorMessage(e) }
  finally { loading.value = false }
}
async function loadRepositories() {
  links.value = []
  commits.value = []
  branches.value = []
  activeLinkId.value = ''
  if (!projectId.value) return
  loading.value = true
  error.value = ''
  const requestedProjectId = projectId.value
  try {
    const found = await githubApi.list(requestedProjectId)
    if (projectId.value === requestedProjectId) links.value = found
  }
  catch (e) { error.value = errorMessage(e) }
  finally { loading.value = false }
}
async function link() {
  if (!isStudent.value || !projectId.value || !repositoryUrl.value.trim() || busy.value) return
  busy.value = true
  error.value = ''; message.value = ''
  try {
    await githubApi.link(projectId.value, repositoryUrl.value.trim())
    repositoryUrl.value = ''
    await loadRepositories()
    message.value = 'Đã liên kết repository GitHub với Project.'
  } catch (e) { error.value = errorMessage(e) }
  finally { busy.value = false }
}
async function removeLink(link: GithubRepositoryLink) {
  if (!isStudent.value || busy.value || !window.confirm(`Gỡ liên kết ${link.repositoryUrl} khỏi Project?`)) return
  busy.value = true
  error.value = ''; message.value = ''
  try {
    await githubApi.unlink(link.id)
    await loadRepositories()
    message.value = 'Đã gỡ liên kết. Repository trên GitHub không bị xóa.'
  } catch (e) { error.value = errorMessage(e) }
  finally { busy.value = false }
}
async function showCommits(link: GithubRepositoryLink) {
  if (loadingCommits.value) return
  activeLinkId.value = link.id
  loadingCommits.value = true
  commits.value = []
  branches.value = []
  error.value = ''
  try {
    const [recent, foundBranches] = await Promise.all([githubApi.commits(link.id), githubApi.branches(link.id)])
    commits.value = recent.items
    branches.value = foundBranches.items
  }
  catch (e) { error.value = errorMessage(e) }
  finally { loadingCommits.value = false }
}
watch(projectId, () => { void loadRepositories() })
watch(() => route.query.projectId, id => {
  if (typeof id === 'string' && projects.value.some(p => p.id === id)) projectId.value = id
})
onMounted(() => { void loadProjects() })
</script>

<template>
  <section class="intro">
    <span class="section-chip">Tích hợp · v0.10</span>
    <h2>GitHub repositories</h2>
    <p>Sinh viên liên kết repository public với đồ án. Giảng viên xem branch mặc định và các commit gần đây.</p>
  </section>

  <p v-if="error" class="gh-notice gh-error" role="alert">{{ error }}</p>
  <p v-if="message" class="gh-notice gh-success" role="status">{{ message }}</p>

  <article class="panel gh-panel">
    <h3>Project</h3>
    <p v-if="loading">Đang tải...</p>
    <p v-if="!loading && !projects.length" class="gh-muted">Chưa có Project để liên kết repository.</p>
    <div v-if="projects.length === 1 && isStudent" class="gh-current">
      <strong>{{ projects[0].topicTitle }}</strong>
      <span>{{ projects[0].lecturerName }}</span>
    </div>
    <label v-else-if="projects.length" class="gh-label">Chọn Project
      <select v-model="projectId" :disabled="busy || loading">
        <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.topicTitle }} · {{ p.studentName }}</option>
      </select>
    </label>
    <p v-if="activeProject" class="gh-muted">Sinh viên: {{ activeProject.studentName }} · Giảng viên: {{ activeProject.lecturerName }}</p>
  </article>

  <article v-if="activeProject" class="panel gh-panel">
    <h3>Repository của Project</h3>
    <form v-if="isStudent" class="gh-add" @submit.prevent="link">
      <label class="gh-label" for="gh-url">URL GitHub repository (public)
        <input id="gh-url" v-model.trim="repositoryUrl" required type="url"
          placeholder="https://github.com/owner/repository" :disabled="busy" maxlength="250" />
      </label>
      <button type="submit" class="action-primary" :disabled="busy || !repositoryUrl.trim()">{{ busy ? 'Đang xử lý…' : 'Liên kết repository' }}</button>
    </form>
    <p v-else class="gh-muted">Giảng viên và Admin chỉ xem. Sinh viên sở hữu Project quản lý liên kết.</p>
    <p class="gh-muted">Phiên bản này chỉ đọc repository public, không yêu cầu hoặc lưu mật khẩu, token GitHub.</p>
    <div class="table-scroller">
      <table class="gh-table">
        <thead><tr><th>Repository</th><th>Branch mặc định</th><th>Cập nhật gần nhất</th><th>Thao tác</th></tr></thead>
        <tbody>
          <tr v-for="r in links" :key="r.id">
            <td>
              <a v-if="safeGithubLink(r.repositoryUrl)" :href="safeGithubLink(r.repositoryUrl)" target="_blank" rel="noopener noreferrer">{{ r.repositoryUrl.replace('https://github.com/', '') }}</a>
              <span v-else>{{ r.repositoryUrl }}</span>
            </td>
            <td>{{ r.defaultBranch || 'Chưa có' }}</td>
            <td>{{ dateTime(r.lastCommitAt) }}</td>
            <td class="gh-actions">
              <button type="button" :disabled="busy || loadingCommits" @click="showCommits(r)">{{ activeLinkId === r.id ? 'Làm mới commit' : 'Xem commit' }}</button>
              <button v-if="isStudent" type="button" :disabled="busy" @click="removeLink(r)">Gỡ liên kết</button>
            </td>
          </tr>
          <tr v-if="!links.length"><td colspan="4" class="gh-empty">Chưa có repository nào được liên kết.</td></tr>
        </tbody>
      </table>
    </div>
  </article>

  <article v-if="activeLink" class="panel gh-panel">
    <h3>Hoạt động GitHub · {{ activeLink.repositoryUrl.replace('https://github.com/', '') }}</h3>
    <p class="gh-muted">Branch gần đây (tối đa 20): {{ branches.length ? branches.join(' · ') : 'Chưa có' }}</p>
    <h4>20 commit gần nhất trên branch mặc định</h4>
    <p v-if="loadingCommits" class="gh-muted">Đang tải commit từ GitHub...</p>
    <p v-else-if="!commits.length" class="gh-muted">Chưa có commit trên branch mặc định, hoặc chưa có dữ liệu.</p>
    <div v-else class="table-scroller">
      <table class="gh-table">
        <thead><tr><th>Commit</th><th>Nội dung</th><th>Người thực hiện</th><th>Thời gian</th></tr></thead>
        <tbody>
          <tr v-for="c in commits" :key="c.sha">
            <td><a v-if="safeGithubLink(c.htmlUrl)" :href="safeGithubLink(c.htmlUrl)" target="_blank" rel="noopener noreferrer">{{ c.sha.slice(0, 7) }}</a><span v-else>{{ c.sha.slice(0, 7) }}</span></td>
            <td class="gh-message">{{ c.message.split('\n')[0] }}</td>
            <td>{{ c.author || 'Không rõ' }}</td>
            <td>{{ dateTime(c.committedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </article>
</template>

<style scoped>
.gh-panel { margin-bottom: 18px; }
.gh-panel h3 { margin-bottom: 13px; }
.gh-label { display: grid; gap: 8px; font-size: 13px; font-weight: 600; color: #4f617c; }
.gh-label input, .gh-label select { width: 100%; min-height: 40px; background: #fff; border: 1px solid #d8e0eb; border-radius: 7px; padding: 9px 12px; color: #27354b; }
.gh-add { display: flex; flex-wrap: wrap; align-items: end; gap: 12px; margin: 12px 0; }
.gh-add .gh-label { flex: 1; min-width: 260px; }
.gh-add button { min-height: 40px; }
.gh-muted { font-size: 13px; color: #758499; line-height: 1.6; }
.gh-current { display: flex; flex-wrap: wrap; gap: 12px; font-size: 14px; }
.gh-current span { color: #758499; }
.gh-table td { border-bottom: 1px solid #edf0f6; padding: 13px; font-size: 13px; vertical-align: top; }
.gh-table a { color: #244fb9; text-decoration: underline; overflow-wrap: anywhere; }
.gh-table .gh-message { white-space: normal; overflow-wrap: anywhere; min-width: 190px; }
.gh-actions { white-space: nowrap; }
.gh-actions button { border: 1px solid #cdd9eb; padding: 7px 9px; border-radius: 6px; background: #fff; color: #254fb0; margin-right: 6px; cursor: pointer; }
.gh-actions button:disabled { opacity: .55; cursor: default; }
.gh-notice { padding: 12px; border-radius: 8px; font-size: 13px; }
.gh-error { background: #fff2f2; color: #9d2525; }
.gh-success { background: #eefaf0; color: #28663f; }
.gh-empty { padding: 35px; text-align: center; color: #8391a7; }
@media (max-width: 600px) { .gh-add .gh-label { min-width: 100%; } }
</style>
