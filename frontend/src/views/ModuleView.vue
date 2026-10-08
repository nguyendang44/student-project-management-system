<script setup lang="ts">
import { computed } from 'vue'
import { moduleById, type ModuleId } from '../app/modules'
const props = defineProps<{ moduleId: ModuleId }>()
const item = computed(() => moduleById(props.moduleId))
</script>
<template>
  <section class="intro"><span class="section-chip">{{ item.group }} · {{ item.requirements }}</span><h2>{{ item.title }}</h2><p>{{ item.description }}</p></section>
  <div class="detail-grid">
    <article class="panel"><div class="panel-heading"><div><h3>Danh sách / dữ liệu</h3><p>Khung danh sách, chưa có kết nối dữ liệu.</p></div><button type="button" class="muted-button" disabled title="Chưa triển khai">+ Thêm mới</button></div>
      <div class="table-scroller"><table><thead><tr><th v-for="column in item.columns" :key="column">{{ column }}</th></tr></thead><tbody><tr><td :colspan="item.columns.length" class="empty-cell"><div class="empty-icon">□</div><strong>Chưa có dữ liệu</strong><p>Danh sách và thao tác sẽ được xây dựng ở giai đoạn triển khai.</p></td></tr></tbody></table></div>
    </article>
    <aside class="panel info-panel"><h3>Phạm vi của module</h3><div class="info-label">Use Case</div><div class="tag-list"><span v-for="uc in item.useCases" :key="uc" class="tag">{{ uc }}</span></div>
      <div class="info-label">API dự kiến</div><code v-for="endpoint in item.endpoints" :key="endpoint" class="endpoint">{{ endpoint }}</code>
      <p class="disclaimer">Các endpoint được khai báo trong backend nhưng hiện trả về 501 Not Implemented.</p>
    </aside>
  </div>
</template>
