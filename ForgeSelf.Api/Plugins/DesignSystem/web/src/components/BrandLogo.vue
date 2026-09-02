<script setup lang="ts">
/**
 * 品牌标识（Logo）· Mark + Wordmark · 节点符号。
 *
 * 设计系统通用组件：Mark 的描边与填充一律消费 `--ds-brand-*` / `--ds-accent-*` 槽位，
 * 因此宿主把它放进「已换肤」的容器时，Logo 会随当前设计系统变色
 * （插件外壳是中性主题，所以壳 Header 里的 Logo 取中性色；展示页里则取生成系统的色）。
 *
 * 注意：SVG 渐变 id 必须唯一，避免同页多实例互相串色。
 */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    variant?: 'mark' | 'wordmark' | 'full'
    size?: number
  }>(),
  { variant: 'full', size: 32 },
)

const wordSize = computed(() => Math.round(props.size * 0.5) + 2)
const gradId = `ds-mark-grad-${Math.random().toString(36).slice(2, 9)}`
</script>

<template>
  <span class="ds-logo" :class="`ds-logo--${variant}`">
    <svg
      v-if="variant !== 'wordmark'"
      class="ds-logo__mark"
      :width="size"
      :height="size"
      viewBox="0 0 32 32"
      fill="none"
      aria-hidden="true"
    >
      <defs>
        <linearGradient :id="gradId" x1="4" y1="4" x2="28" y2="28" gradientUnits="userSpaceOnUse">
          <stop class="ds-logo__stop-a" />
          <stop class="ds-logo__stop-b" offset="1" />
        </linearGradient>
      </defs>
      <path
        d="M16 6 26 24 6 24Z"
        :stroke="`url(#${gradId})`"
        stroke-width="1.8"
        stroke-linejoin="round"
        opacity="0.5"
      />
      <circle cx="16" cy="6" r="3.4" class="ds-logo__c-a" />
      <circle cx="26" cy="24" r="3.4" class="ds-logo__c-b" />
      <circle cx="6" cy="24" r="3.4" class="ds-logo__c-c" />
    </svg>
    <span v-if="variant !== 'mark'" class="ds-logo__word" :style="{ fontSize: wordSize + 'px' }">ForgeSelf</span>
  </span>
</template>

<style scoped>
.ds-logo {
  display: inline-flex;
  align-items: center;
  gap: var(--ds-space-2);
}
/* 渐变与节点全部取主题槽位：换肤即变色 */
.ds-logo__stop-a {
  stop-color: var(--ds-brand-500);
}
.ds-logo__stop-b {
  stop-color: var(--ds-accent-400);
}
.ds-logo__c-a {
  fill: var(--ds-brand-600);
}
.ds-logo__c-b {
  fill: var(--ds-accent-500);
}
.ds-logo__c-c {
  fill: var(--ds-brand-700);
}
.ds-logo__word {
  font-weight: var(--ds-fw-bold);
  letter-spacing: -0.01em;
  color: var(--ds-brand-wordmark);
}
.ds-logo--mark .ds-logo__word,
.ds-logo--wordmark .ds-logo__mark {
  display: none;
}
</style>
