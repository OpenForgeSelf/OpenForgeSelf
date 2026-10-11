<script setup lang="ts">
/**
 * ForgeCard — 器物玻璃卡片（设计稿 OtherScreens.jsx 统一规格）。
 * 复用宿主全局 .glass（blur/边框/背景），可选 interactive 悬停反馈。
 * 颜色一律 token 引用（禁裸色值，DESIGN.md 门禁）。
 */
withDefaults(
  defineProps<{
    /** 是否可交互（悬停轻微抬升 + 金边，对应设计稿 .interactive） */
    interactive?: boolean
    /** 内边距（px），对应设计稿 glass 卡 padding 14~20 */
    padding?: number
    /** 点击回调（interactive 时常用） */
    onClick?: () => void
  }>(),
  { interactive: false, padding: 16, onClick: undefined },
)
</script>

<template>
  <div
    class="forge-card glass"
    :class="{ 'forge-card--interactive': interactive }"
    :style="{ padding: `${padding}px` }"
    role="button"
    :tabindex="interactive ? 0 : undefined"
    @click="interactive ? onClick?.() : undefined"
    @keydown.enter.prevent="interactive ? onClick?.() : undefined"
  >
    <slot />
  </div>
</template>

<style scoped>
/*
 * 动效刻度（DESIGN.md Motion / workshop-forge.css token，禁硬编码秒数）：
 * - 进入（挂载）= --dur-fast 160ms ease-out（一次性，不循环）
 * - 展开/交互（hover/focus）= --dur-base 220ms ease-out
 * - --ease-forge 锤落回弹仅 logo/重点出现时刻使用，本组件不滥用
 */
.forge-card {
  min-width: 0;
  transition:
    color var(--dur-base) var(--ease-out),
    box-shadow var(--dur-base) var(--ease-out),
    transform var(--dur-base) var(--ease-out);
  animation: forge-card-in var(--dur-fast) var(--ease-out) both;
}

@keyframes forge-card-in {
  from {
    opacity: 0;
    transform: translateY(4px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .forge-card {
    animation: none;
  }
}

.forge-card--interactive {
  cursor: pointer;
}

.forge-card--interactive:hover {
  box-shadow: 0 0 0 1px var(--border-brand), 0 8px 24px color-mix(in srgb, var(--ink-950) 45%, transparent);
}

.forge-card--interactive:focus-visible {
  outline: none;
  box-shadow: 0 0 0 2px var(--gold-400);
}
</style>
