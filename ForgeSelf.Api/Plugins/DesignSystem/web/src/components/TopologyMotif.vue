<script setup lang="ts">
/**
 * 拓扑装饰元件（Topology Motif）· SVG。
 * 设计系统品牌装饰：节点 + 连接线，呼应「节点」Logo 隐喻，用于 Hero / 空态背景。
 *
 * 通用组件：颜色消费 `--ds-brand-*` / `--ds-accent-*` 槽位，随当前设计系统换肤。
 * 渐变 id 唯一，避免同页多实例串色。
 */
withDefaults(defineProps<{ size?: number }>(), { size: 240 })

const gradId = `ds-topo-grad-${Math.random().toString(36).slice(2, 9)}`
</script>

<template>
  <svg
    class="ds-topo"
    :width="size"
    :height="size"
    viewBox="0 0 240 240"
    fill="none"
    aria-hidden="true"
  >
    <defs>
      <linearGradient :id="gradId" x1="0" y1="0" x2="240" y2="240" gradientUnits="userSpaceOnUse">
        <stop class="ds-topo__stop-a" />
        <stop class="ds-topo__stop-b" offset="1" />
      </linearGradient>
    </defs>
    <g class="ds-topo__lines" :stroke="`url(#${gradId})`" stroke-width="1.2" opacity="0.55">
      <path d="M40 60 120 120M120 120 200 70M120 120 150 200M40 60 150 200M200 70 150 200" />
      <path d="M120 120 70 180M120 120 190 170" stroke-dasharray="3 4" />
    </g>
    <g class="ds-topo__nodes" :fill="`url(#${gradId})`">
      <circle cx="40" cy="60" r="6" />
      <circle cx="200" cy="70" r="6" />
      <circle cx="150" cy="200" r="6" />
      <circle cx="70" cy="180" r="4.5" opacity="0.8" />
      <circle cx="190" cy="170" r="4.5" opacity="0.8" />
      <circle cx="120" cy="120" r="9" />
    </g>
  </svg>
</template>

<style scoped>
.ds-topo {
  display: block;
}
.ds-topo__stop-a {
  stop-color: var(--ds-brand-500);
}
.ds-topo__stop-b {
  stop-color: var(--ds-accent-400);
}
</style>
