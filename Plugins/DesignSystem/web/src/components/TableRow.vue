<script setup lang="ts">
/**
 * 服务表格行（Table Row）· status dot + P99。
 */
import StatusBadge from './StatusBadge.vue'

defineProps<{
  name: string
  region: string
  status?: 'healthy' | 'degraded' | 'down' | 'deploying'
  p99: string
  requests?: string
}>()
</script>

<template>
  <div class="ds-row-item">
    <div class="ds-row-item__name">
      <span class="ds-row-item__svc">{{ name }}</span>
      <span class="ds-micro">{{ region }}</span>
    </div>
    <div class="ds-row-item__badge">
      <StatusBadge :status="status" />
    </div>
    <div class="ds-row-item__p99 ds-num">{{ p99 }}</div>
    <div class="ds-row-item__req ds-small">{{ requests ?? '—' }}</div>
  </div>
</template>

<style scoped>
.ds-row-item {
  display: grid;
  grid-template-columns: 1.6fr 1fr 0.8fr 1fr;
  align-items: center;
  gap: var(--ds-space-4);
  padding: var(--ds-space-3) var(--ds-space-4);
  border-bottom: 1px solid var(--ds-border-1);
}
.ds-row-item:last-child {
  border-bottom: none;
}
.ds-row-item__name {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ds-row-item__svc {
  font-weight: var(--ds-fw-semibold);
  color: var(--ds-fg-1);
}
.ds-row-item__p99 {
  font-weight: var(--ds-fw-semibold);
  color: var(--ds-fg-1);
  font-variant-numeric: tabular-nums;
}
.ds-row-item__req {
  color: var(--ds-fg-3);
  font-variant-numeric: tabular-nums;
}
</style>
