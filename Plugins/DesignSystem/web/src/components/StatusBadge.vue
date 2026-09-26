<script setup lang="ts">
/**
 * 状态徽章（Status Badge / Chip）。
 * 健康 / 降级 / 故障 / 部署中 四种语义状态。
 */
import Icon from './Icon.vue'

const props = withDefaults(
  defineProps<{
    status?: 'healthy' | 'degraded' | 'down' | 'deploying'
    label?: string
  }>(),
  { status: 'healthy' },
)

const META: Record<string, { text: string; icon: string; dot: string; bg: string; fg: string }> = {
  healthy: { text: '健康', icon: 'check', dot: 'var(--ds-success)', bg: 'var(--ds-success-soft)', fg: 'var(--ds-success)' },
  degraded: { text: '降级', icon: 'alert-triangle', dot: 'var(--ds-warning)', bg: 'var(--ds-warning-soft)', fg: 'var(--ds-warning)' },
  down: { text: '故障', icon: 'alert-octagon', dot: 'var(--ds-danger)', bg: 'var(--ds-danger-soft)', fg: 'var(--ds-danger)' },
  deploying: { text: '部署中', icon: 'loader', dot: 'var(--ds-info)', bg: 'var(--ds-info-soft)', fg: 'var(--ds-info)' },
}
const m = META[props.status]
</script>

<template>
  <span class="ds-badge" :style="{ background: m.bg, color: m.fg }">
    <Icon :name="m.icon" :size="14" />
    <span class="ds-badge__label">{{ label ?? m.text }}</span>
    <span class="ds-badge__dot" :class="{ 'ds-badge__dot--spin': status === 'deploying' }" :style="{ background: m.dot }" />
  </span>
</template>

<style scoped>
.ds-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--ds-space-2);
  padding: 4px var(--ds-space-3);
  border-radius: var(--ds-radius-pill);
  font-size: var(--ds-fs-small);
  font-weight: var(--ds-fw-medium);
  line-height: 1;
}
.ds-badge__dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}
.ds-badge__dot--spin {
  animation: ds-spin 1s linear infinite;
}
@keyframes ds-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
