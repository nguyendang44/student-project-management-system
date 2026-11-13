<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HttpError } from '../api/client'
import { useAuthStore } from '../stores/auth'
import { projectProgressApi, type ProjectV07 } from '../features/progress/projectProgress.api'
import { githubApi } from '../features/github/github.api'
import type { GithubRepositoryLink, GithubCommit } from '../features/github/github.types'
import { latestTwentyValidCommits, isCommitOrderValid, formatCommitChoice } from '../features/gitProgress/gitProgress.commitSelection'
import { gitProgressApi, type GitProgressRun, type GitProgressRunListItem,
  type GitProgressFile, type GitProgressEvidence, type GitProgressAiDiagnostics } from '../features/gitProgress/gitProgress.api'

const route = useRoute()
const auth = useAuthStore()
const projects = ref<ProjectV07[]>([])
const projectId = ref('')
const repos = ref<GithubRepositoryLink[]>([])
const repositoryId = ref('')
const commits = ref<GithubCommit[]>([])
const manualShaMode = ref(false)
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
// GitHub returns commits newest first. When selecting inside the list, Base
// must precede Head. The backend separately verifies real ancestry/merges.
const commitOrderOkay = computed(() => manualShaMode.value ||
  isCommitOrderValid(commits.value, baseSha.value, headSha.value))
const ready = computed(() => !!repositoryId.value && canRun.value && shaOkay(baseSha.value) &&
  shaOkay(headSha.value) && baseSha.value.toLowerCase() !== headSha.value.toLowerCase() &&
  commitOrderOkay.value)
const selected = computed(() => repos.value.find(r => r.id === repositoryId.value))
const files = computed<GitProgressFile[]>(() => parseArray<GitProgressFile>(active.value?.filesJson))
const evidence = computed<GitProgressEvidence[]>(() => parseArray<GitProgressEvidence>(active.value?.evidenceJson))
const aiDiagnostics = computed<GitProgressAiDiagnostics | null>(() => active.value?.aiDiagnostics ?? null)
const aiUnavailable = computed(() => (!!aiDiagnostics.value?.analysisStatus && aiDiagnostics.value.analysisStatus !== 'available') ||
  aiDiagnostics.value?.featureReportStatus === 'error')
const rejectionLabels: { key: keyof GitProgressAiDiagnostics['rejections']; label: string }[] = [
  { key: 'unknownId', label: 'Mã bằng chứng AI không tồn tại' },
  { key: 'unsentFile', label: 'File không có trong dữ liệu gửi AI' },
  { key: 'unsentLine', label: 'Dòng mã không khớp diff đã gửi' },
  { key: 'nonFunctionalLine', label: 'Dòng mã không mô tả hành vi' },
  { key: 'testSetupOnly', label: 'Chỉ chuẩn bị dữ liệu kiểm thử' },
  { key: 'groupedTestDetail', label: 'Đã gộp vào nhóm kiểm thử hành vi' },
  { key: 'groupedFeatureDetail', label: 'Bằng chứng đã nằm trong nhóm công việc chính' },
  { key: 'selectionLimit', label: 'Không được chọn do giới hạn số lượng theo file/nhóm' },
  { key: 'invalidLength', label: 'Mô tả quá ngắn hoặc quá dài' },
  { key: 'unsupportedClaim', label: 'Kết luận thiếu bằng chứng' },
  { key: 'semanticMismatch', label: 'Mô tả không được dòng code trích dẫn chứng minh' },
  { key: 'supportingDetail', label: 'Dòng phụ trợ (import/biến test/metadata), không tính công việc riêng' },
  { key: 'boilerplate', label: 'Mô tả chung chung hoặc lặp ý' },
  { key: 'missingAction', label: 'Thiếu hành động cụ thể' },
  { key: 'duplicate', label: 'Nhận xét trùng lặp' },
]
const rejectionRows = computed(() => rejectionLabels
  .map(x => ({ label: x.label, value: aiDiagnostics.value?.rejections?.[x.key] ?? 0 }))
  .filter(x => x.value > 0))
const aiDiagnosticSummary = computed(() => {
  const d = aiDiagnostics.value
  if (!d) return 'Báo cáo cũ chưa lưu dữ liệu chẩn đoán. Hãy tạo báo cáo mới để xem phần tổng hợp chức năng.'
  if (d.analysisStatus === 'timeout') return 'Ollama quá thời gian chờ. Dữ kiện Git vẫn được giữ; bật Ollama rồi phân tích lại cùng cặp commit.'
  if (d.analysisStatus === 'connection-error') return 'Không kết nối được Ollama. Dữ kiện Git vẫn được giữ; kiểm tra Ollama rồi phân tích lại.'
  if (d.analysisStatus === 'invalid-response') return 'Ollama trả phản hồi lỗi hoặc sai cấu trúc. Dữ kiện Git vẫn được giữ; có thể phân tích lại.'
  if (d.sentFiles === 0) return 'Không có đoạn diff phù hợp được gửi cho Ollama.'
  if (evidence.value.some(x => x.source === 'diff-feature-report'))
    return 'Các nhóm AI chỉ chứng minh tuyến API hoặc chưa đủ căn cứ. Báo cáo hiển thị từng thao tác API có bằng chứng từ Git diff; không phải danh sách chức năng đã hoàn thành.'
  if (d.featureReportStatus === 'accepted') return `AI đã tổng hợp ${d.featureReportAccepted ?? d.featureGroups} chức năng/công việc từ các dòng thay đổi có bằng chứng.`
  if (d.featureReportStatus === 'empty') return 'AI chưa tìm thấy đủ bằng chứng để xác định chức năng riêng biệt; không có nghĩa sinh viên không làm việc.'
  if (d.featureReportStatus === 'filtered') return 'AI có trả nhóm chức năng nhưng chưa nhóm nào vượt qua kiểm tra bằng chứng. Các thay đổi Git vẫn được lưu.'
  if (d.featureReportStatus === 'error') return 'Không tạo được báo cáo chức năng từ Ollama; hệ thống giữ báo cáo bằng chứng Git dự phòng.'
  if (d.modelItems === 0) return 'Ollama không trả nhận xét chi tiết; các file vẫn có thể đã được đọc hết.'
  return 'AI đã đọc diff; chưa có báo cáo chức năng được xác minh.'
})
const explanationDiagnosticSummary = computed(() => {
  const d = aiDiagnostics.value
  if (!d) return ''
  if (evidence.value.some(x => x.source === 'diff-feature-report'))
    return `Ollama có ${d.featureReportAccepted ?? 0} nhóm đạt bộ lọc gốc; báo cáo sử dụng các thao tác API đối chiếu trực tiếp từ Git để tránh gộp hoặc đặt tên sai.`
  if (d.featureReportStatus === 'accepted') return `Đã giữ ${d.featureReportAccepted ?? 0} chức năng có các bước và trích dẫn Git.`
  if (d.featureReportStatus === 'empty') return 'Không có nhóm chức năng đủ bằng chứng để lập báo cáo.'
  if (d.featureReportStatus === 'filtered') return 'Các nhóm AI trả về bị loại vì thiếu trích dẫn hợp lệ hoặc mô tả vượt quá bằng chứng.'
  if (d.featureReportStatus === 'error') return 'Lượt tổng hợp chức năng bị lỗi; dữ kiện Git không bị xóa.'
  return 'Chưa có dữ liệu tổng hợp chức năng.'
})
// New reports use one card per user-visible feature; old report shapes remain readable.
type DisplayWork = { item: GitProgressEvidence; explanation: string | null }
const aiFeatureItems = computed(() => evidence.value.filter(x =>
  x.source === 'ai-feature-report' || x.source === 'diff-feature-report'))
const hasDiffFeatureFallback = computed(() => aiFeatureItems.value.some(x => x.source === 'diff-feature-report'))
const hasAiFeatureReport = computed(() => aiFeatureItems.value.length > 0)
const workItems = computed<DisplayWork[]>(() => {
  if (aiFeatureItems.value.length > 0)
    return aiFeatureItems.value.map(item => ({ item, explanation: null }))
  const rows = evidence.value
  const oldExplainers = rows.filter(x => x.source === 'ollama-feature')
  const featureFacts = rows.filter(x => x.source === 'feature-rule')
  const primary = featureFacts.length > 0 ? featureFacts : rows.filter(x => x.source !== 'ollama-feature')
  return primary.map(item => {
    let explanation = item.aiExplanation?.trim() || null
    if (!explanation && item.source === 'feature-rule') {
      const related = oldExplainers.find(x =>
        x.area === item.area && x.file === item.file &&
        x.title === `AI diễn giải: ${item.title || ''}`)
      explanation = related?.description?.trim() || null
    }
    return { item, explanation }
  })
})
const evidenceGroups = computed(() => {
  const groups = new Map<string, DisplayWork[]>()
  for (const row of workItems.value) {
    const area = row.item.area || areaFromPath(row.item.file)
    if (!groups.has(area)) groups.set(area, [])
    groups.get(area)!.push(row)
  }
  const order = ['API', 'AI/Backend', 'Frontend', 'Kiểm thử', 'CSDL', 'Backend khác']
  return Array.from(groups, ([area, items]) => ({ area, items }))
    .sort((a, b) => {
      const x = order.indexOf(a.area), y = order.indexOf(b.area)
      return (x < 0 ? 99 : x) - (y < 0 ? 99 : y)
    })
})
const groupedFeatureCount = computed(() => hasAiFeatureReport.value
  ? aiFeatureItems.value.length : workItems.value.filter(x => x.item.source === 'feature-rule').length)
const supplemental = computed(() => groupedFeatureCount.value > 0
  ? evidence.value.filter(x => !['feature-rule', 'ollama-feature', 'ai-feature-report', 'diff-feature-report'].includes(x.source || '')) : [])
// This label is deterministic from path/type. It is NOT an AI judgment
// about completion, passing tests, or functionality running in production.
function supplementalKind(row: GitProgressEvidence): string {
  const path = row.file
  if (/IntegrationTests\/|\/tests\/|\.spec\.ts$/i.test(path)) return 'Mã kiểm thử'
  if (/^scripts\/|\.(?:sql|tf|json|yml|yaml|csproj)$/i.test(path)) return 'Script / cấu hình'
  if (/^frontend\//i.test(path)) return 'Tích hợp giao diện'
  if (/\/Endpoints\/|\/Program\.cs$/i.test(path)) return 'Khai báo API / cấu hình dịch vụ'
  if (/\/Services\//i.test(path)) return 'Mã xử lý backend'
  return 'Chi tiết kỹ thuật'
}
const supplementalGroups = computed(() => {
  const groups = new Map<string, GitProgressEvidence[]>()
  for (const item of supplemental.value) {
    const label = supplementalKind(item)
    if (!groups.has(label)) groups.set(label, [])
    groups.get(label)!.push(item)
  }
  return Array.from(groups, ([label, items]) => ({ label, items }))
})
const explanationCount = computed(() => workItems.value.filter(x => x.explanation && x.item.source === 'feature-rule').length)
function areaFromPath(path: string): string {
  if (/IntegrationTests\/|\/tests\/|\.spec\.ts$/i.test(path)) return 'Kiểm thử'
  if (path.startsWith('frontend/')) return 'Frontend'
  if (/\/Migrations\/|Persistence\/|\/Entities\//i.test(path)) return 'CSDL'
  if (/\/Endpoints\/|\/Program\.cs$/i.test(path)) return 'API'
  if (/\/Services\//i.test(path)) return 'AI/Backend'
  return 'Backend khác'
}
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
async function retryAi() {
  if (!active.value || busy.value || loading.value || !canRun.value || !aiUnavailable.value) return
  manualShaMode.value = true
  baseSha.value = active.value.baseSha
  headSha.value = active.value.headSha
  await start()
}
function downloadReport() {
  if (!active.value) return
  const blob = new Blob([JSON.stringify({ ...active.value, files: files.value, evidence: evidence.value }, null, 2)],
    { type: 'application/json;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `git-progress-${active.value.baseSha.slice(0, 8)}-${active.value.headSha.slice(0, 8)}.json`
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
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
  manualShaMode.value = false
  if (!repositoryId.value) return
  loading.value = true; error.value = ''; info.value = ''
  try {
    // SQL history should remain visible if GitHub API is rate-limited.
    runs.value = await gitProgressApi.list(repositoryId.value)
    if (runs.value[0]) await show(runs.value[0].id)
    try {
      commits.value = latestTwentyValidCommits((await githubApi.commits(repositoryId.value)).items)
      headSha.value = commits.value[0]?.sha || ''
      baseSha.value = commits.value[1]?.sha || ''
      if (commits.value.length < 2)
        info.value = 'Repository chưa có đủ hai commit để chọn. Có thể bật nhập SHA thủ công nếu cần.'
    } catch (_e) { info.value = 'Không đọc được commit GitHub. Có thể bật nhập SHA thủ công nếu cần.' }
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
      <p>AI nhóm các thay đổi ở nhiều file thành từng chức năng sinh viên đã thực hiện, giải thích cách triển khai và dẫn dòng Git để giảng viên đối chiếu.</p>
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
      <label class="gp-manual-toggle">
        <input v-model="manualShaMode" type="checkbox" :disabled="busy || loading" />
        Nhập SHA thủ công (ngoài 20 commit gần nhất)
      </label>
      <label>Commit cũ (Base)
        <input v-if="manualShaMode" v-model.trim="baseSha" placeholder="SHA đủ 40 ký tự" autocomplete="off"
          :disabled="busy || loading" />
        <select v-else v-model="baseSha" :disabled="busy || loading || commits.length < 2">
          <option value="" disabled>Chọn commit cũ</option>
          <option v-for="c in commits" :key="c.sha" :value="c.sha">{{ formatCommitChoice(c) }}</option>
        </select>
      </label>
      <label>Commit mới (Head)
        <input v-if="manualShaMode" v-model.trim="headSha" placeholder="SHA đủ 40 ký tự" autocomplete="off"
          :disabled="busy || loading" />
        <select v-else v-model="headSha" :disabled="busy || loading || commits.length < 2">
          <option value="" disabled>Chọn commit mới</option>
          <option v-for="c in commits" :key="c.sha" :value="c.sha">{{ formatCommitChoice(c) }}</option>
        </select>
      </label>
      <p v-if="!manualShaMode && commits.length > 0 && !commitOrderOkay" class="gp-selection-error">
        Commit cũ phải nằm trước commit mới trong danh sách GitHub.
      </p>
      <button :disabled="!ready || busy || loading" @click="start">{{ busy ? 'Đang gửi...' : 'Phân tích tiến độ' }}</button>
      <button class="secondary" :disabled="loading || !repositoryId" @click="loadHistory">Làm mới</button>
    </div>
    <p class="hint">Danh sách lấy tối đa 20 commit gần nhất của branch mặc định trên GitHub (SHA · nội dung · tác giả · ngày).
      Mặc định chọn 2 commit mới nhất. Có thể bật nhập SHA thủ công để so sánh mốc cũ hơn.
      Backend luôn xác minh commit mới là hậu duệ của commit cũ trước khi phân tích.</p>
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
            <button class="secondary" @click="downloadReport">Tải kết quả JSON</button>
            <p v-if="aiDiagnostics?.analysisStatus && aiDiagnostics.analysisStatus !== 'available'" class="alert" role="status">
              {{ aiDiagnosticSummary }} Báo cáo Git hoàn tất, phần AI chưa hoàn tất.
            </p>
            <button v-if="aiUnavailable && canRun" class="secondary" :disabled="busy || loading" @click="retryAi">
              Thử lại AI cho báo cáo này
            </button>
            <div class="metrics">
              <div><b>{{ active.totalCommits }}</b><small>Commit mới</small></div>
              <div><b>{{ active.totalFiles }}</b><small>File thay đổi</small></div>
              <div><b>+{{ active.additions }}</b><small>Dòng thêm</small></div>
              <div><b>-{{ active.deletions }}</b><small>Dòng xóa</small></div>
            </div>
            <p>{{ active.summary }}</p>
            <p class="hint">AI {{ active.modelName }}
              <span v-if="!aiUnavailable">· đã gửi đoạn diff của {{ active.sentFiles }} file.</span>
              <span v-else>· chưa hoàn tất lượt phân tích.</span>
              <span v-if="active.partialCoverage">Phạm vi GitHub trả về có thể chưa đầy đủ.</span>
            </p>
            <p v-if="compareLink()"><a :href="compareLink()" target="_blank" rel="noopener noreferrer">Xem đối chiếu gốc trên GitHub</a></p>
            <p v-if="aiDiagnostics?.contextStatus" class="hint">
              <template v-if="aiDiagnostics.contextExcerpts">
                Đã lấy được {{ aiDiagnostics.contextExcerpts }} đoạn code ngữ cảnh của {{ aiDiagnostics.contextFiles }} file
                tại các commit đã chọn. Đây là mẫu có giới hạn, không phải toàn bộ dự án.
                <span v-if="aiDiagnostics.contextStatus === 'partial'">Chỉ lấy được ngữ cảnh ở một mốc; chưa đủ để đối chiếu cả hai phiên bản.</span>
              </template>
              <template v-else>Không lấy được code ngữ cảnh phù hợp; kết quả chỉ dựa trên diff.</template>
            </p>
            <details class="gp-ai-diagnostics">
              <summary>Chi tiết kiểm tra AI {{ aiUnavailable ? '(chưa hoàn tất)' : aiDiagnostics ? `(${aiDiagnostics.verified}/${aiDiagnostics.parsedCandidates} nhận xét hợp lệ)` : '(báo cáo cũ)' }}</summary>
              <p>{{ aiDiagnosticSummary }}</p>
              <template v-if="aiDiagnostics">
                <p v-if="aiDiagnostics.coverageStatus && aiDiagnostics.coverageStatus !== 'unknown'" class="hint">
                  <b>Phạm vi đọc diff:</b>
                  {{ aiDiagnostics.fullyAnalyzedFiles ?? 0 }}/{{ aiDiagnostics.eligibleFiles ?? 0 }} file đủ điều kiện
                  được Ollama xử lý hết patch an toàn;
                  {{ aiDiagnostics.completedBatches ?? 0 }}/{{ aiDiagnostics.plannedBatches ?? 0 }} lượt hoàn tất.
                  <template v-if="aiDiagnostics.coverageStatus === 'partial'">
                    Phân tích một phần:
                    {{ aiDiagnostics.missingPatchFiles ?? 0 }} file thiếu patch,
                    {{ aiDiagnostics.skippedLines ?? 0 }} dòng được giữ lại ngoài prompt vì an toàn/độ dài,
                    {{ aiDiagnostics.truncatedBatches ?? 0 }} đoạn vượt giới hạn,
                    {{ aiDiagnostics.failedBatches ?? 0 }} lượt gọi chưa hoàn tất.
                  </template>
                  <template v-else-if="aiDiagnostics.coverageStatus === 'no-readable-patch'">
                    Không có patch văn bản phù hợp để gửi cho AI.
                  </template>
                  <template v-else>Không còn đoạn patch an toàn nào bị bỏ qua trong số file đủ điều kiện.</template>
                </p>
                <div v-if="!aiUnavailable" class="gp-diag-metrics">
                  <span>Diff đã đọc: <b>{{ aiDiagnostics.sentFiles }} file</b></span>
                  <span>Dòng bằng chứng dùng để nhóm chức năng: <b>{{ aiDiagnostics.featureEvidenceLines ?? 0 }}</b></span>
                  <span>Chức năng được xác minh trích dẫn: <b>{{ aiDiagnostics.featureReportAccepted ?? 0 }}</b></span>
                </div>
                <p v-if="aiDiagnostics.modelItems > aiDiagnostics.parsedCandidates" class="hint">
                  {{ aiDiagnostics.modelItems - aiDiagnostics.parsedCandidates }} item thiếu cấu trúc tối thiểu.
                </p>
                <p v-if="aiDiagnostics.ruleFallbackItems > 0" class="hint">
                  Có {{ aiDiagnostics.ruleFallbackItems }} phát hiện dự phòng từ quy tắc, không phải nhận xét Ollama.
                </p>
                <p v-if="rejectionRows.length"><b>Phân loại nhận xét sau kiểm tra (các nhóm loại trừ nhau):</b></p>
                <ul v-if="rejectionRows.length">
                  <li v-for="row in rejectionRows" :key="row.label">{{ row.label }}: {{ row.value }}</li>
                </ul>
                <p v-if="rejectionRows.length" class="hint">
                  Tổng mô tả chưa đạt yêu cầu: {{ aiDiagnostics.rejections.weakDescription }}
                  (gồm mô tả quá dài/ngắn, thiếu căn cứ, sai ý nghĩa dòng code, chung chung hoặc thiếu hành động).
                  {{ aiDiagnostics.rejections.eligible }} nhận xét còn lại sau khi chọn và gộp;
                  không cộng weakDescription thêm lần nữa.
                </p>
                <p><b>Báo cáo chức năng theo mục tiêu người dùng:</b> {{ explanationDiagnosticSummary }}</p>
                <p class="hint">
                  Ollama trả {{ aiDiagnostics.featureReportModelItems ?? 0 }} nhóm,
                  {{ aiDiagnostics.featureReportCandidates ?? 0 }} nhóm đủ cấu trúc,
                  {{ aiDiagnostics.featureReportAccepted ?? 0 }} nhóm được giữ;
                  {{ aiDiagnostics.featureReportRejectedEvidence ?? 0 }} tham chiếu không được sử dụng (sai ID, không an toàn hoặc không hỗ trợ mô tả).
                </p>
                <p v-if="(aiDiagnostics.featureEvidenceOmitted ?? 0) > 0" class="hint">
                  Còn {{ aiDiagnostics.featureEvidenceOmitted }} dòng bằng chứng chưa đưa vào lượt gom chức năng do giới hạn prompt; cần đọc phần danh sách file và diff để đối chiếu bổ sung.
                </p>
              </template>
              <p class="hint">Chỉ lưu số lượng và nhóm lý do; không lưu câu trả lời thô của Ollama trong chẩn đoán.</p>
            </details>
            <h4>{{ hasDiffFeatureFallback ? 'Các thao tác API có bằng chứng Git (đối chiếu)' : hasAiFeatureReport ? 'AI tổng hợp các chức năng sinh viên đã thực hiện' : 'Các công việc có bằng chứng từ thay đổi mã nguồn' }} ({{ groupedFeatureCount || workItems.length }})</h4>
            <p class="hint" v-if="hasDiffFeatureFallback">
              Những nhóm chỉ dựa vào khai báo tuyến API được tách thành thao tác riêng. Mỗi mục trích dòng tuyến API và
              có thể kèm mã frontend/handler liên quan trực tiếp nếu có dòng thay đổi tương ứng; không chứng minh chức năng đã chạy đúng.
            </p>
            <p class="hint" v-else-if="hasAiFeatureReport">
              Một phiên bản có thể có nhiều chức năng. AI gom các thay đổi frontend, backend, dữ liệu và kiểm thử
              cùng phục vụ một mục tiêu thành một chức năng; mỗi bước đều có dòng Git để đối chiếu.
            </p>
            <p class="hint" v-else>
              Chưa tạo được báo cáo chức năng AI đủ căn cứ. Các nhóm dự phòng dưới đây chỉ mô tả thay đổi thấy trong Git diff.
            </p>
            <p v-if="!workItems.length">Không có nhóm chức năng đủ bằng chứng trong phạm vi diff đã chọn; điều này không chứng minh sinh viên chưa làm việc.</p>
            <section v-for="group in evidenceGroups" :key="group.area" class="evidence-group">
              <h5 v-if="!hasAiFeatureReport">{{ group.area }} · {{ group.items.length }} {{ groupedFeatureCount ? 'nhóm công việc' : 'nhận xét' }}</h5>
              <article v-for="(row, i) in group.items" :key="row.item.featureId || row.item.factId || i" class="evidence feature-report-card">
                <b v-if="row.item.title" class="feature-report-title">{{ row.item.title }}</b>
                <p v-if="hasAiFeatureReport"><strong>Sinh viên đã làm gì?</strong> {{ row.item.description }}</p>
                <p v-else>{{ row.item.description }}</p>
                <div v-if="hasAiFeatureReport && row.item.components?.length" class="feature-components">
                  <span v-for="component in row.item.components" :key="component">{{ component }}</span>
                </div>
                <div v-if="hasAiFeatureReport && row.item.technologies?.length" class="feature-technologies">
                  <strong>Công nghệ/cơ chế có bằng chứng:</strong>
                  <span v-for="technology in row.item.technologies" :key="technology">{{ technology }}</span>
                </div>
                <div v-if="hasAiFeatureReport && row.item.steps?.length" class="feature-steps-block">
                  <strong>Cách triển khai theo bằng chứng code</strong>
                  <ol class="feature-steps">
                    <li v-for="(step, stepIndex) in row.item.steps" :key="stepIndex">
                      <p>{{ step.detail }}</p>
                      <details class="feature-step-evidence">
                        <summary>Bằng chứng bước này ({{ step.citations?.length ?? 0 }} dòng)</summary>
                        <div v-for="(citation, citationIndex) in step.citations" :key="citationIndex" class="gp-citation">
                          <small>{{ citation.file }}</small>
                          <pre>{{ citation.evidence }}</pre>
                        </div>
                      </details>
                    </li>
                  </ol>
                </div>
                <p v-if="hasAiFeatureReport && row.item.verificationStatus" class="feature-verification">
                  <strong>Mức xác minh:</strong> {{ row.item.verificationStatus }}
                </p>
                <details v-if="hasAiFeatureReport && row.item.citations?.length" class="feature-all-evidence">
                  <summary>Tất cả file và dòng Git liên quan ({{ row.item.citations.length }})</summary>
                  <div v-for="(citation, j) in row.item.citations" :key="j" class="gp-citation">
                    <small>{{ citation.file }}</small>
                    <pre>{{ citation.evidence }}</pre>
                  </div>
                </details>
                <div v-else-if="!hasAiFeatureReport && row.item.citations?.length" class="gp-citations">
                  <div v-for="(citation, j) in row.item.citations" :key="j" class="gp-citation">
                    <small>{{ citation.file }}</small>
                    <pre>{{ citation.evidence }}</pre>
                  </div>
                </div>
                <template v-else-if="!hasAiFeatureReport"><b>{{ row.item.file }}</b><pre>{{ row.item.evidence }}</pre></template>
                <div v-if="row.explanation" class="gp-ai-note">
                  <strong>Ý nghĩa do AI diễn giải (tham khảo)</strong>
                  <p>{{ row.explanation }}</p>
                </div>
              </article>
            </section>
            <details v-if="supplemental.length" class="gp-supplemental">
              <summary>Chi tiết kỹ thuật đã đối chiếu diff ({{ supplemental.length }}, không tính thành {{ supplemental.length }} chức năng)</summary>
              <p class="hint">Phân nhóm theo đường dẫn nguồn, không suy ra mức hoàn thành. Chỉ xác nhận dòng được thêm; kiểm thử có mã không đồng nghĩa đã chạy đạt.</p>
              <section v-for="group in supplementalGroups" :key="group.label">
                <h5>{{ group.label }} · {{ group.items.length }} chi tiết</h5>
                <article v-for="(extra, j) in group.items" :key="j" class="evidence">
                  <p>{{ extra.description }}</p>
                  <small>{{ extra.file }}</small>
                  <pre>{{ extra.evidence }}</pre>
                </article>
              </section>
            </details>
            <details>
              <summary>Danh sách file sau lọc ({{ files.length }}/{{ active.totalFiles }})</summary>
              <p v-if="active.totalFiles > files.length" class="hint">
                GitHub trả về {{ active.totalFiles }} file; {{ active.totalFiles - files.length }} file
                không được đưa vào danh sách chi tiết theo chính sách đường dẫn/bản sao lưu.
                Số liệu Git ở trên vẫn bao gồm các file đó.
              </p>
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
.gp-controls .gp-manual-toggle { flex: 1 1 100%; display: flex; align-items: center; gap: .5rem; }
.gp-controls .gp-manual-toggle input { min-height: 0; width: auto; }
.gp-selection-error { flex-basis: 100%; margin: 0; color: #b91c1c; font-size: .9rem; }
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
.evidence-group { margin-top: 1rem; }
.evidence-group h5 { margin: .6rem 0; font-size: 1rem; }
.evidence { border-top: 1px solid #cbd5e1; padding-top: .8rem; margin-top: .8rem; }
.feature-report-card { padding: 1rem; border: 1px solid #cbd5e1; border-radius: 10px; background: var(--surface,#fff); }
.feature-report-title { display: block; font-size: 1.08rem; margin-bottom: .45rem; }
.feature-components { display: flex; flex-wrap: wrap; gap: .4rem; margin: .65rem 0; }
.feature-components span { border: 1px solid #94a3b8; border-radius: 999px; padding: .2rem .55rem; font-size: .8rem; }
.feature-technologies { display: flex; flex-wrap: wrap; align-items: center; gap: .4rem; margin: .65rem 0; font-size: .9rem; }
.feature-technologies span { background: rgba(37,99,235,.08); border-radius: 5px; padding: .2rem .5rem; }
.feature-steps-block { margin-top: .8rem; }
.feature-steps { padding-left: 1.35rem; display: grid; gap: .65rem; }
.feature-steps li > p { margin: .25rem 0; }
.feature-step-evidence,.feature-all-evidence { margin-top: .35rem; font-size: .9rem; }
.feature-step-evidence summary,.feature-all-evidence summary { cursor: pointer; color: var(--text-muted,#64748b); }
.feature-verification { margin-top: .8rem; padding: .6rem .75rem; border-radius: 6px; background: rgba(100,116,139,.08); font-size: .9rem; }
.gp-ai-diagnostics { border: 1px solid #cbd5e1; border-radius: 8px; padding: .7rem .9rem; background: rgba(100,116,139,.04); }
.gp-ai-diagnostics summary { cursor: pointer; font-weight: 600; }
.gp-ai-diagnostics p { margin: .55rem 0; }
.gp-ai-diagnostics ul { margin: .3rem 0 .8rem; padding-left: 1.5rem; }
.gp-diag-metrics { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: .35rem; font-size: .9rem; }
@media (max-width: 600px) { .gp-diag-metrics { grid-template-columns: 1fr; } }
.gp-ai-note { border-left: 3px solid #64748b; padding: .55rem .85rem; margin: .7rem 0; background: rgba(100,116,139,.06); border-radius: 0 6px 6px 0; }
.gp-ai-note strong { font-size: .9rem; }
.gp-ai-note p { margin: .4rem 0 0; }
.evidence pre { white-space: pre-wrap; overflow-wrap: anywhere; background: rgba(100,116,139,.09); padding: .7rem; border-radius: 6px; }
.gp-citations { display: grid; gap: .45rem; }
.gp-citation small { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; overflow-wrap: anywhere; color: var(--text-muted,#64748b); }
.gp-citation pre { margin: .2rem 0 .55rem; }
.file-line { display: flex; justify-content: space-between; flex-wrap: wrap; gap: .8rem; border-top: 1px solid #cbd5e1; padding: .4rem; }
.file-line small { color: var(--text-muted,#64748b); }
.err { color: #b91c1c; }
</style>
