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
const rejectionLabels: { key: keyof GitProgressAiDiagnostics['rejections']; label: string }[] = [
  { key: 'unknownId', label: 'Mã bằng chứng AI không tồn tại' },
  { key: 'unsentFile', label: 'File không có trong dữ liệu gửi AI' },
  { key: 'unsentLine', label: 'Dòng mã không khớp diff đã gửi' },
  { key: 'nonFunctionalLine', label: 'Dòng mã không mô tả hành vi' },
  { key: 'testSetupOnly', label: 'Chỉ chuẩn bị dữ liệu kiểm thử' },
  { key: 'groupedTestDetail', label: 'Đã gộp vào nhóm kiểm thử hành vi' },
  { key: 'groupedFeatureDetail', label: 'Bằng chứng đã nằm trong nhóm công việc chính' },
  { key: 'invalidLength', label: 'Mô tả quá ngắn hoặc quá dài' },
  { key: 'unsupportedClaim', label: 'Kết luận thiếu bằng chứng' },
  { key: 'boilerplate', label: 'Mô tả chung chung hoặc lặp ý' },
  { key: 'missingAction', label: 'Thiếu hành động cụ thể' },
  { key: 'duplicate', label: 'Nhận xét trùng lặp' },
]
const rejectionRows = computed(() => rejectionLabels
  .map(x => ({ label: x.label, value: aiDiagnostics.value?.rejections?.[x.key] ?? 0 }))
  .filter(x => x.value > 0))
const aiDiagnosticSummary = computed(() => {
  const d = aiDiagnostics.value
  if (!d) return 'Báo cáo cũ chưa lưu dữ liệu chẩn đoán. Hãy chạy lại bằng v3.9.3 để xem lý do.'
  if (d.sentFiles === 0) return 'Không có đoạn diff phù hợp được gửi cho Ollama.'
  if (d.modelItems === 0) return 'Ollama trả danh sách nhận xét rỗng (items=[]); không có nhận xét để kiểm duyệt.'
  if (d.parsedCandidates === 0) return 'Ollama có trả item, nhưng tất cả thiếu evidenceId hoặc description hợp lệ.'
  if (d.verified === 0) return 'Ollama có đề xuất, nhưng không có nhận xét nào vượt qua kiểm tra bằng chứng và chất lượng.'
  return `Đã giữ ${d.verified} nhận xét AI có bằng chứng; các nhận xét khác bị kiểm duyệt nếu không đạt yêu cầu.`
})
const explanationDiagnosticSummary = computed(() => {
  const d = aiDiagnostics.value
  if (!d) return ''
  if (d.explanationStatus === 'no-feature-groups') return 'Không có nhóm công việc để yêu cầu AI diễn giải.'
  if (d.explanationStatus === 'error') return 'Không đọc được diễn giải AI; nhóm công việc từ Git diff vẫn được giữ.'
  if (d.explanationStatus === 'empty') return 'Ollama trả danh sách diễn giải mục đích rỗng.'
  if (d.explanationStatus === 'invalid-items') return 'Các item diễn giải thiếu factId hoặc explanation hợp lệ.'
  if (d.explanationStatus === 'filtered') return 'Các diễn giải được tạo nhưng chưa đủ căn cứ hoặc chưa nêu mục đích mới.'
  if (d.explanationStatus === 'accepted') return `Đã giữ ${d.explanationAccepted} diễn giải tham khảo.`
  return 'Chưa yêu cầu diễn giải AI.'
})
// v3.7 stores the optional explanation inside the same rule-derived fact.
// v3.6 history stored each AI explanation as a separate row; join old rows by
// verified area/title/file for display without changing the historical JSON.
type DisplayWork = { item: GitProgressEvidence; explanation: string | null }
const workItems = computed<DisplayWork[]>(() => {
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
const groupedFeatureCount = computed(() => workItems.value.filter(x => x.item.source === 'feature-rule').length)
const supplemental = computed(() => groupedFeatureCount.value > 0
  ? evidence.value.filter(x => x.source !== 'feature-rule' && x.source !== 'ollama-feature') : [])
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
      <p>So sánh Git diff, lưu báo cáo và hiển thị những thay đổi có bằng chứng, phân biệt nhận xét AI với dữ kiện trích xuất trực tiếp.</p>
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
            <p v-if="aiDiagnostics?.contextStatus" class="hint">
              <template v-if="aiDiagnostics.contextExcerpts">
                AI đã nhận thêm {{ aiDiagnostics.contextExcerpts }} đoạn code ngữ cảnh của {{ aiDiagnostics.contextFiles }} file
                tại các commit đã chọn. Đây là mẫu có giới hạn, không phải toàn bộ dự án.
                <span v-if="aiDiagnostics.contextStatus === 'partial'">Chỉ lấy được ngữ cảnh ở một mốc; chưa đủ để đối chiếu cả hai phiên bản.</span>
              </template>
              <template v-else>Không lấy được code ngữ cảnh phù hợp; kết quả chỉ dựa trên diff.</template>
            </p>
            <details class="gp-ai-diagnostics">
              <summary>Chi tiết kiểm tra AI {{ aiDiagnostics ? `(${aiDiagnostics.verified}/${aiDiagnostics.parsedCandidates} nhận xét hợp lệ)` : '(báo cáo cũ)' }}</summary>
              <p>{{ aiDiagnosticSummary }}</p>
              <template v-if="aiDiagnostics">
                <div class="gp-diag-metrics">
                  <span>Đoạn diff gửi AI: <b>{{ aiDiagnostics.sentFiles }} file</b></span>
                  <span>Item AI trả về: <b>{{ aiDiagnostics.modelItems }}</b></span>
                  <span>Ứng viên đủ trường: <b>{{ aiDiagnostics.parsedCandidates }}</b></span>
                  <span>Nhận xét được chấp nhận: <b>{{ aiDiagnostics.verified }}</b></span>
                </div>
                <p v-if="aiDiagnostics.modelItems > aiDiagnostics.parsedCandidates" class="hint">
                  {{ aiDiagnostics.modelItems - aiDiagnostics.parsedCandidates }} item thiếu cấu trúc tối thiểu.
                </p>
                <p v-if="aiDiagnostics.ruleFallbackItems > 0" class="hint">
                  Có {{ aiDiagnostics.ruleFallbackItems }} phát hiện dự phòng từ quy tắc, không phải nhận xét Ollama.
                </p>
                <p v-if="rejectionRows.length"><b>Lý do loại nhận xét (đếm theo lý do đầu tiên):</b></p>
                <ul v-if="rejectionRows.length">
                  <li v-for="row in rejectionRows" :key="row.label">{{ row.label }}: {{ row.value }}</li>
                </ul>
                <p><b>Diễn giải AI cho {{ aiDiagnostics.featureGroups }} nhóm công việc:</b> {{ explanationDiagnosticSummary }}</p>
                <p class="hint">
                  Diễn giải: {{ aiDiagnostics.explanationModelItems }} item trả về,
                  {{ aiDiagnostics.explanationCandidates }} ứng viên đủ trường,
                  {{ aiDiagnostics.explanationAccepted }} được giữ.
                </p>
              </template>
              <p class="hint">Chỉ lưu số lượng và nhóm lý do; không lưu câu trả lời thô của Ollama trong chẩn đoán.</p>
            </details>
            <h4>Những công việc được nhận diện từ thay đổi mã nguồn ({{ groupedFeatureCount || workItems.length }})</h4>
            <p class="hint">Bằng chứng Git chỉ xác nhận mã đã thay đổi, không xác nhận hành vi chạy thực tế. Diễn giải AI chỉ là nội dung tham khảo gắn với nhóm đã đối chiếu.</p>
            <p v-if="groupedFeatureCount" class="hint">{{ groupedFeatureCount }} nhóm công việc; {{ explanationCount }} nhóm có diễn giải AI bổ sung.</p>
            <p v-if="!workItems.length">Không có nhận xét đủ bằng chứng trong phạm vi diff đã chọn; không có nghĩa sinh viên chưa làm việc.</p>
            <section v-for="group in evidenceGroups" :key="group.area" class="evidence-group">
              <h5>{{ group.area }} · {{ group.items.length }} {{ groupedFeatureCount ? 'nhóm công việc' : 'nhận xét' }}</h5>
              <article v-for="(row, i) in group.items" :key="row.item.factId || i" class="evidence">
                <b v-if="row.item.title">{{ row.item.title }}</b>
                <p>{{ row.item.description }}</p>
                <div v-if="row.item.citations?.length" class="gp-citations">
                  <div v-for="(citation, j) in row.item.citations" :key="j" class="gp-citation">
                    <small>{{ citation.file }}</small>
                    <pre>{{ citation.evidence }}</pre>
                  </div>
                </div>
                <template v-else><b>{{ row.item.file }}</b><pre>{{ row.item.evidence }}</pre></template>
                <div v-if="row.explanation" class="gp-ai-note">
                  <strong>Ý nghĩa do AI diễn giải (tham khảo)</strong>
                  <p>{{ row.explanation }}</p>
                </div>
              </article>
            </section>
            <details v-if="supplemental.length" class="gp-supplemental">
              <summary>Nhận xét bổ sung từ diff ({{ supplemental.length }}, không tính vào số nhóm công việc)</summary>
              <article v-for="(extra, j) in supplemental" :key="j" class="evidence">
                <p>{{ extra.description }}</p>
                <small>{{ extra.file }}</small>
                <pre>{{ extra.evidence }}</pre>
              </article>
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
