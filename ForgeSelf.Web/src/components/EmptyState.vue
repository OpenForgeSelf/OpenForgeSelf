<script setup lang="ts">
/**
 * EmptyState — 空态分级（设计稿 OtherScreens.jsx 统一规格）。
 * 结构：图标（28px 金）+ 主文案（serif 14px）+ 副文案（mono 10px）+ 可选引导/重试操作。
 * 颜色一律 token 引用（禁裸色值，DESIGN.md 门禁）。
 */
withDefaults(
  defineProps<{
    /** lucide 图标名（经 lucideIconSvg 渲染）或 SVG 片段 */
    icon?: string
    /** 主文案（serif） */
    title?: string
    /** 副文案（mono 小字） */
    sub?: string
  }>(),
  { icon: '', title: '', sub: '' },
)
</script>

<template>
  <div class="empty-state">
    <div v-if="icon" class="empty-state__icon">
      <slot name="icon"><span v-html="icon" class="empty-state__icon-svg" /></slot>
    </div>
    <div v-if="title" class="empty-state__title">{{ title }}</div>
    <div v-if="sub" class="empty-state__sub">{{ sub }}</div>
    <div v-if="$slots.action" class="empty-state__action">
      <slot name="action" />
    </div>
  </div>
</template>

<style scoped>
.empty-state {
  padding: 28px 20px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.empty-state__icon {
  width: 52px;
  height: 52px;
  border-radius: 12px;
  background: var(--gold-tint);
  color: var(--gold-400);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 4px;
}

.empty-state__icon-svg :deep(svg) {
  width: 28px;
  height: 28px;
}

.empty-state__title {
  font-family: var(--font-serif-cn, serif);
  font-size: 14px;
  color: color-mix(in srgb, var(--fg-primary) 70%, transparent);
}

.empty-state__sub {
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  color: color-mix(in srgb, var(--fg-primary) 40%, transparent);
  letter-spacing: 0.03em;
}

.empty-state__action {
  margin-top: 4px;
}
</style>
