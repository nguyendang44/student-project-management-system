<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { gitProgressApi, type GitProgressRun,
  type WordComparisonResult } from '../features/gitProgress/gitProgress.api'

const props = defineProps<{ report: GitProgressRun | null }>()
const file = ref<File | null>(null)
const result = ref<WordComparisonResult | null>(null)
const error = ref('')
const busy = ref(false)
const selected = computed(() => props.report?.status === 'COMPLETED' ? props.report : null)
let comparisonRequest: AbortController | undefined

function selectFile(event: Event) {
  file.value = (event.target as HTMLInputElement).files?.item(0) ?? null
  result.value = null
  error.value = ''
}
async function compare() {
  if (!selected.value || !file.value || busy.value) return
  const request = new AbortController()
  const reportId = selected.value.id
  comparisonRequest = request
  busy.value = true
  result.value = null
  error.value = ''
  try {
    const comparison = await gitProgressApi.compareWord(reportId, file.value, request.signal)
    if (!request.signal.aborted && selected.value?.id === reportId) result.value = comparison
  } catch (exception) {
    if (!request.signal.aborted)
      error.value = exception instanceof Error ? exception.message : 'Không đối chiếu được báo cáo Word.'
  } finally {
    if (comparisonRequest === request) { comparisonRequest = undefined; busy.value = false }
  }
}

watch(() => selected.value?.id, () => {
  comparisonRequest?.abort()
  comparisonRequest = undefined
  file.value = null
  result.value = null
  error.value = ''
  busy.value = false
}, { flush: 'sync' })
onUnmounted(() => comparisonRequest?.abort())
</script>

<template>
  <section class="word-panel" aria-labelledby="word-heading">
    <h3 id="word-heading">Đối chiếu báo cáo Word</h3>
    <p v-if="!selected">Chọn báo cáo Git ở phần chức năng để đối chiếu.</p>
    <p v-else class="word-hint">Dùng báo cáo đang hiển thị · {{ selected.baseSha.slice(0, 8) }} → {{ selected.headSha.slice(0, 8) }}</p>
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
