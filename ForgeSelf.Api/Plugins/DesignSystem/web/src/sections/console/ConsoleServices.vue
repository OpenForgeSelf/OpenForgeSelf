<script setup lang="ts">
/** 控制台 · Services 服务列表。 */
import TableRow from '../../components/TableRow.vue'
import StatusBadge from '../../components/StatusBadge.vue'
import DsInput from '../../components/DsInput.vue'
import Icon from '../../components/Icon.vue'

const rows = [
  { name: 'api-gateway', region: 'cn-east-1', status: 'healthy' as const, p99: '42ms', requests: '9.4k/s' },
  { name: 'auth-service', region: 'cn-east-1', status: 'degraded' as const, p99: '118ms', requests: '3.1k/s' },
  { name: 'user-service', region: 'cn-east-1', status: 'healthy' as const, p99: '56ms', requests: '5.2k/s' },
  { name: 'worker-pool', region: 'us-west-2', status: 'deploying' as const, p99: '—', requests: '—' },
  { name: 'billing-svc', region: 'eu-central', status: 'down' as const, p99: '—', requests: '0' },
  { name: 'search-index', region: 'eu-central', status: 'healthy' as const, p99: '73ms', requests: '2.0k/s' },
]
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div class="ds-row ds-wrap ds-gap-3" style="justify-content: space-between">
      <div><h2 class="ds-h2">服务 Services</h2><div class="ds-small">6 个服务 · 跨区域部署</div></div>
      <DsInput placeholder="搜索服务…" style="min-width: 240px" />
    </div>

    <div class="ds-surface ds-row ds-gap-3" style="padding: var(--ds-space-3) var(--ds-space-4); color: var(--ds-fg-3)">
      <Icon name="server" :size="16" />
      <span class="ds-small">状态图例：</span>
      <StatusBadge status="healthy" label="健康" />
      <StatusBadge status="degraded" label="降级" />
      <StatusBadge status="deploying" label="部署中" />
      <StatusBadge status="down" label="故障" />
    </div>

    <div class="ds-surface" style="padding: 0; overflow: hidden">
      <div class="ds-row-item" style="background: var(--ds-surface-2); font-weight: var(--ds-fw-semibold); color: var(--ds-fg-3)">
        <div>服务</div><div>状态</div><div>P99</div><div>吞吐</div>
      </div>
      <TableRow v-for="r in rows" :key="r.name" v-bind="r" />
    </div>
  </div>
</template>
