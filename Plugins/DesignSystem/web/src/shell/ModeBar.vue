<script setup lang="ts">
/**
 * 四模式切换条（v3 外壳）。
 *
 * §U DOM 契约（e2e / 验收判据）：容器 `role="tablist" aria-label="模式"`；
 * 每个按钮 `role="tab"`、可访问名**恰为** 开始 / 展厅 / 工作台 / 交付与接入、
 * `aria-selected` 表达当前模式；左右方向键在 tab 间循环切换并移动焦点（WAI-ARIA tabs 模式，AC23）。
 * 切换由父组件写回 `ds.mode`（FR2），本组件只发事件不做持久化。
 */
import { nextTick, ref } from 'vue'
import { MODES, MODE_LABELS, type Mode } from './mode'

const props = defineProps<{ modelValue: Mode }>()
const emit = defineEmits<{ 'update:modelValue': [mode: Mode] }>()

const bar = ref<HTMLElement | null>(null)

function select(m: Mode): void {
  emit('update:modelValue', m)
}

/** 方向键循环切换；切换后焦点随选中项移动（保证方向键连续可用） */
function move(step: number): void {
  const i = MODES.indexOf(props.modelValue)
  const next = MODES[(i + step + MODES.length) % MODES.length]
  select(next)
  void nextTick(() => {
    bar.value?.querySelector<HTMLElement>('.ds-modebar__tab[aria-selected="true"]')?.focus()
  })
}
</script>

<template>
  <div ref="bar" class="ds-modebar" role="tablist" aria-label="模式">
    <button
      v-for="m in MODES"
      :key="m"
      class="ds-modebar__tab"
      :class="{ 'ds-modebar__tab--active': modelValue === m }"
      type="button"
      role="tab"
      :aria-selected="modelValue === m"
      @click="select(m)"
      @keydown.left.prevent="move(-1)"
      @keydown.right.prevent="move(1)"
      @keydown.home.prevent="select(MODES[0])"
      @keydown.end.prevent="select(MODES[MODES.length - 1])"
    >
      {{ MODE_LABELS[m] }}
    </button>
  </div>
</template>

<style scoped>
.ds-modebar {
  display: flex;
  align-items: center;
  gap: var(--ds-space-1);
  padding: 0 var(--ds-space-6);
  border-bottom: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
}
.ds-modebar__tab {
  font: inherit;
  font-size: var(--ds-fs-small);
  border: 1px solid transparent;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--ds-fg-2);
  padding: var(--ds-space-2) var(--ds-space-4);
  cursor: pointer;
  border-radius: var(--ds-radius-sm) var(--ds-radius-sm) 0 0;
}
.ds-modebar__tab:hover {
  color: var(--ds-fg-1);
}
.ds-modebar__tab--active {
  color: var(--ds-color-primary);
  border-bottom-color: var(--ds-color-primary);
  background: var(--ds-surface-1);
}
.ds-modebar__tab:focus-visible {
  outline: 2px solid var(--ds-color-primary);
  outline-offset: -2px;
}
</style>
