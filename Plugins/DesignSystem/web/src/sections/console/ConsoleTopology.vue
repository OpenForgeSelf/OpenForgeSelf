<script setup lang="ts">
/** 控制台 · Topology 拓扑视图。 */
import TopologyMotif from '../../components/TopologyMotif.vue'
import Icon from '../../components/Icon.vue'

const nodes = [
  { id: 'gateway', label: 'api-gateway', x: 60, y: 40, color: 'var(--ds-brand-600)' },
  { id: 'auth', label: 'auth-service', x: 200, y: 40, color: 'var(--ds-accent-500)' },
  { id: 'user', label: 'user-service', x: 130, y: 130, color: 'var(--ds-brand-600)' },
  { id: 'worker', label: 'worker-pool', x: 60, y: 220, color: 'var(--ds-accent-500)' },
  { id: 'billing', label: 'billing-svc', x: 200, y: 220, color: 'var(--ds-gray-400)' },
]
const edges = [
  ['gateway', 'auth'],
  ['gateway', 'user'],
  ['user', 'worker'],
  ['user', 'billing'],
  ['auth', 'billing'],
]
function pos(id: string) {
  return nodes.find((n) => n.id === id)!
}
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div><h2 class="ds-h2">拓扑 Topology</h2><div class="ds-small">服务依赖与调用关系</div></div>

    <div class="ds-surface ds-row ds-gap-5" style="padding: var(--ds-space-6); align-items: center">
      <svg class="ds-topo-svg" viewBox="0 0 260 260" width="260" height="260" fill="none">
        <g stroke="var(--ds-border-3)" stroke-width="1.5">
          <path v-for="(e, i) in edges" :key="i" :d="`M${pos(e[0]).x} ${pos(e[0]).y} L${pos(e[1]).x} ${pos(e[1]).y}`" />
        </g>
        <g>
          <g v-for="n in nodes" :key="n.id">
            <circle :cx="n.x" :cy="n.y" r="9" :fill="n.color" />
            <text :x="n.x" :y="n.y + 26" text-anchor="middle" font-size="10" fill="var(--ds-fg-3)">{{ n.label }}</text>
          </g>
        </g>
      </svg>

      <div class="ds-stack ds-gap-3">
        <div class="ds-row ds-gap-2"><Icon name="network" :size="18" /><span class="ds-h4">5 个节点 · 5 条依赖边</span></div>
        <div class="ds-small">拓扑装饰元件（Topology Motif）复用于 Hero / 空态。节点颜色沿用 Brand Purple 与 Signal Cyan token。</div>
        <div class="ds-surface-2 ds-small" style="padding: var(--ds-space-3)">
          图例：<span style="color: var(--ds-brand-600)">●</span> 核心服务 ·
          <span style="color: var(--ds-accent-500)">●</span> 数据面 ·
          <span style="color: var(--ds-gray-400)">●</span> 降级 / 离线
        </div>
        <TopologyMotif :size="120" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.ds-topo-svg {
  flex: none;
}
</style>
