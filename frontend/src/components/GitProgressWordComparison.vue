<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { gitProgressApi, type GitProgressRunListItem,
  type WordComparisonResult } from '../features/gitProgress/gitProgress.api'

const props = defineProps<{ repositoryId: string; projectId: string; requestedRunId: string }>()
const reports = ref<GitProgressRunListItem[]>([])
const reportId = ref('')
const loading = ref(false)
const listError = ref('')
const file = ref<File | null>(null)
const result = ref<WordComparisonResult | null>(null)
const error = ref('')
const busy = ref(false)
const selected = computed(() => reports.value.find(report => report.id === reportId.value))
let listRequest: AbortController | undefined
let comparisonRequest: AbortController | undefined

async function loadReports() {
  listRequest?.abort()
  reportId.value = ''
  reports.value = []
  listError.value = ''
  if (!props.repositoryId) { loading.value = false; return }
  const request = new AbortController()
  listRequest = request
  loading.value = true
  try {
    const history = await gitProgressApi.list(props.repositoryId, request.signal)
    if (request.signal.aborted) return
    reports.value = history.filter(report => report.status === 'COMPLETED')
    reportId.value = reports.value.find(report => report.id === props.requestedRunId)?.id ?? ''
    if (!reportId.value && props.requestedRunId)
      listError.value = 'Báo cáo từ liên kết không còn trong danh sách hoàn tất của repository này. Hãy chọn một báo cáo khác.'
  } catch (exception) {
    if (!request.signal.aborted)
      listError.value = exception instanceof Error ? exception.message : 'Không tải được báo cáo Git.'
  } finally {
    if (listRequest === request) { listRequest = undefined; loading.value = false }
  }
}
function selectFile(event: Event) {
  file.value = (event.target as HTMLInputElement).files?.item(0) ?? null
  result.value = null
  error.value = ''
}
async function compare() {
  if (!selected.value || !file.value || busy.value) return
  const request = new AbortController()
  comparisonRequest = request
  busy.value = true
  result.value = null
  error.value = ''
  try {
    const comparison = await gitProgressApi.compareWord(selected.value.id, file.value, request.signal)
    if (!request.signal.aborted) result.value = comparison
  } catch (exception) {
    if (!request.signal.aborted)
      error.value = exception instanceof Error ? exception.message : 'Không đối chiếu được báo cáo Word.'
  } finally {
    if (comparisonRequest === request) { comparisonRequest = undefined; busy.value = false }
  }
}

watch(() => [props.repositoryId, props.requestedRunId], loadReports, { immediate: true })
watch(reportId, () => {
  comparisonRequest?.abort()
  file.value = null
  result.value = null
  error.value = ''
  busy.value = false
})
onUnmounted(() => {
  listRequest?.abort()
  comparisonRequest?.abort()
})
</script>

<template>
  <section class="word-panel" aria-labelledby="word-heading">
    <h3 id="word-heading">Đối chiếu báo cáo Word</h3>
    <p v-if="!repositoryId">Chọn đồ án và repository để xem các báo cáo Git đã hoàn tất.</p>
    <p v-else-if="loading" role="status">Đang tải báo cáo Git…</p>
    <p v-if="listError" role="alert" class="word-error">{{ listError }}</p>
    <p v-if="repositoryId && !loading && !reports.length && !listError">
      Repository này chưa có báo cáo Git đã hoàn tất.
    </p>
    <RouterLink v-if="repositoryId && !loading && !reports.length"
      :to="{ path: '/git-progress', query: { projectId } }">Mở Phân tích tiến độ Git</RouterLink>
    <label v-if="reports.length" class="word-field">
      Báo cáo Git đã hoàn tất
      <select v-model="reportId">
        <option value="" disabled>Chọn cặp commit</option>
        <option v-for="report in reports" :key="report.id" :value="report.id">
          {{ report.baseSha.slice(0, 8) }} → {{ report.headSha.slice(0, 8) }} ·
          {{ new Date(report.completedAt || report.createdAt).toLocaleString('vi-VN') }} · {{ report.modelName }}
        </option>
      </select>
    </label>
    <form v-if="selected" :key="selected.id" class="word-form" @submit.prevent="compare">
      <label class="word-field">
        Báo cáo Word (.docx, tối đa 2 MB)
        <input type="file" accept=".docx" :disabled="busy" @change="selectFile">
      </label>
      <button type="submit" :disabled="!file || busy">
        {{ busy ? 'Đang đối chiếu…' : 'Đối chiếu với báo cáo Git' }}
      </button>
    </form>
    <p v-if="error" role="alert" class="word-error">{{ error }}</p>
    <section v-if="result" aria-live="polite">
      <h4>Tỷ lệ đối chiếu {{ result.score }}%</h4>
      <p>{{ result.full }} đầy đủ · {{ result.partial }} một phần · {{ result.missing }} chưa tìm thấy</p>
      <p class="word-hint">Từng mục Word được tính với trọng số 1, 0,5 hoặc 0. Mở dòng Git để kiểm tra kết quả gợi ý.</p>
      <ol class="word-results">
        <li v-for="row in result.items" :key="row.id">
          <strong>{{ { full: 'Có bằng chứng', partial: 'Có bằng chứng một phần', missing: 'Chưa tìm thấy bằng chứng' }[row.status] }}</strong>
          <p>{{ row.claim }}</p>
          <details v-if="row.evidence.length">
            <summary>Xem dòng Git</summary>
            <div v-for="citation in row.evidence" :key="citation.id">
              <p class="word-hint">{{ citation.title }} · {{ citation.file }}</p>
              <pre>{{ citation.evidence }}</pre>
            </div>
          </details>
        </li>
      </ol>
    </section>
  </section>
</template>

<style scoped>
.word-panel { border: 1px solid #cbd5e1; border-radius: 10px; padding: 1rem; min-width: 0; }
.word-panel h3 { margin: 0 0 .75rem; }
.word-form { display: flex; flex-wrap: wrap; align-items: end; gap: .75rem; margin: 1rem 0; }
.word-field { display: grid; gap: .35rem; min-width: 0; }
.word-field select, .word-field input { max-width: 100%; }
.word-error { color: #b91c1c; }
.word-hint { color: var(--text-muted, #475569); overflow-wrap: anywhere; }
.word-results { padding-left: 1.5rem; }
.word-results li { padding: .7rem 0; border-top: 1px solid #cbd5e1; }
.word-results pre { white-space: pre-wrap; overflow-wrap: anywhere; background: rgba(100,116,139,.09); padding: .7rem; }
</style>
