<script setup lang="ts">
/**
 * 指标卡（Metric Card）· 服务总数 / 集群负载 等控制台关键指标。
 * 数值走 tabular-nums（指标数字 token）。
 */
import type { PropType } from 'vue'

defineProps<{
  label: string
  value: string | number
  unit?: string
  delta?: string
  trend?: 'up' | 'down' | 'flat'
  hint?: string
}>()
</script>

<template>
  <div class="ds-metric">
    <div class="ds-metric__label ds-micro">{{ label }}</div>
    <div class="ds-metric__value ds-num">
      {{ value }}<span v-if="unit" class="ds-metric__unit">{{ unit }}</span>
    </div>
    <div class="ds-metric__foot">
      <span
        v-if="delta"
        class="ds-metric__delta"
        :class="`ds-metric__delta--${trend ?? 'flat'}`"
        >{{ delta }}</span
      >
      <span v-if="hint" class="ds-small">{{ hint }}</span>
    </div>
  </div>
</template>

<style scoped>
.ds-metric {
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  padding: var(--ds-space-5);
  box-shadow: var(--ds-shadow-sm);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.ds-metric__label {
  color: var(--ds-fg-3);
}
.ds-metric__value {
  font-size: var(--ds-fs-h2);
  font-weight: var(--ds-fw-bold);
  color: var(--ds-fg-1);
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.ds-metric__unit {
  font-size: var(--ds-fs-body);
  font-weight: var(--ds-fw-medium);
  color: var(--ds-fg-3);
  margin-left: 4px;
}
.ds-metric__foot {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
}
.ds-metric__delta {
  font-size: var(--ds-fs-small);
  font-weight: var(--ds-fw-semibold);
}
.ds-metric__delta--up {
  color: var(--ds-success);
}
.ds-metric__delta--down {
  color: var(--ds-danger);
}
.ds-metric__delta--flat {
  color: var(--ds-fg-4);
}
</style>
