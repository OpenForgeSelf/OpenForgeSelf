<script setup lang="ts">
/** 控制台 · Overview 总览。 */
import MetricCard from '../../components/MetricCard.vue'
import StatusBadge from '../../components/StatusBadge.vue'
import TableRow from '../../components/TableRow.vue'
import Icon from '../../components/Icon.vue'

const metrics = [
  { label: '服务总数', value: 128, delta: '+6', trend: 'up' as const, hint: '近 7 天' },
  { label: '集群负载', value: 62.4, unit: '%', delta: '-3.1%', trend: 'down' as const, hint: 'P99 健康' },
  { label: '健康率', value: 97.6, unit: '%', delta: '+0.4%', trend: 'up' as const },
  { label: '请求 / 分', value: 18.2, unit: 'k', delta: '+1.2k', trend: 'up' as const },
]
const rows = [
  { name: 'api-gateway', region: 'cn-east-1', status: 'healthy' as const, p99: '42ms', requests: '9.4k/s' },
  { name: 'auth-service', region: 'cn-east-1', status: 'degraded' as const, p99: '118ms', requests: '3.1k/s' },
  { name: 'worker-pool', region: 'us-west-2', status: 'deploying' as const, p99: '—', requests: '—' },
  { name: 'billing-svc', region: 'eu-central', status: 'down' as const, p99: '—', requests: '0' },
]
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div class="ds-row ds-wrap ds-gap-3" style="justify-content: space-between">
      <div>
        <h2 class="ds-h2">总览 Overview</h2>
        <div class="ds-small">跨集群服务健康与流量实时快照</div>
      </div>
      <StatusBadge status="healthy" label="系统正常" />
    </div>

    <div class="ds-swatch-grid">
      <MetricCard v-for="m in metrics" :key="m.label" v-bind="m" />
    </div>

    <div class="ds-surface" style="padding: 0; overflow: hidden">
      <div class="ds-row-item" style="background: var(--ds-surface-2); font-weight: var(--ds-fw-semibold); color: var(--ds-fg-3)">
        <div>服务</div><div>状态</div><div>P99</div><div>吞吐</div>
      </div>
      <TableRow v-for="r in rows" :key="r.name" v-bind="r" />
    </div>

    <div class="ds-surface ds-row ds-gap-3" style="padding: var(--ds-space-4)">
      <Icon name="activity" :size="18" />
      <span class="ds-small">提示：本分区为设计系统「控制台 UI Kit」的 Overview 演示，组件均来自 components/ 库。</span>
    </div>
  </div>
</template>
