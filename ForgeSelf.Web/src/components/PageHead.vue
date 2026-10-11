<script setup lang="ts">
/**
 * PageHead — 功能屏页头（设计稿 OtherScreens.jsx 统一规格）。
 * eyebrow：mono 10px 金字 0.18em 大写｜title：display 26px 700｜actions：右侧操作区。
 * 所有颜色引用设计稿 token（--gold-400 / --fg-primary / --font-*），禁裸色值。
 */
defineProps<{
  eyebrow?: string
  title: string
}>()
</script>

<template>
  <div class="page-head">
    <div class="page-head__text">
      <div v-if="eyebrow" class="page-head__eyebrow">{{ eyebrow }}</div>
      <h1 class="page-head__title">{{ title }}</h1>
    </div>
    <div v-if="$slots.actions" class="page-head__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.page-head {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  margin-bottom: 20px;
}

.page-head__text {
  min-width: 0;
}

.page-head__eyebrow {
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  letter-spacing: 0.18em;
  color: var(--gold-400);
  text-transform: uppercase;
  margin-bottom: 4px;
  /* 进入动效 = --dur-base 220ms ease-out（一次性挂载） */
  animation: page-head-in var(--dur-base) var(--ease-out) both;
}

.page-head__title {
  margin: 0;
  font-family: var(--font-display, serif);
  font-size: 26px;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: var(--fg-primary);
  line-height: 1.2;
  /* 标题跟随 eyebrow 进入，--dur-instant 90ms 微延迟形成层级 */
  animation: page-head-in var(--dur-base) var(--ease-out) var(--dur-instant) both;
}

@keyframes page-head-in {
  from {
    opacity: 0;
    transform: translateY(6px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .page-head__eyebrow,
  .page-head__title {
    animation: none;
  }
}

.page-head__actions {
  margin-left: auto;
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}
</style>
