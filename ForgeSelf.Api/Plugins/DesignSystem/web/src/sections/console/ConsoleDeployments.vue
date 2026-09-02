<script setup lang="ts">
/** 控制台 · Deployments 部署。 */
import StatusBadge from '../../components/StatusBadge.vue'
import DsButton from '../../components/DsButton.vue'
import Icon from '../../components/Icon.vue'

const deploys = [
  { name: 'api-gateway', ver: 'v1.4.2', status: 'healthy' as const, time: '2 分钟前' },
  { name: 'worker-pool', ver: 'v0.9.1', status: 'deploying' as const, time: '进行中' },
  { name: 'billing-svc', ver: 'v2.0.0', status: 'down' as const, time: '14 分钟前' },
  { name: 'search-index', ver: 'v1.1.3', status: 'healthy' as const, time: '1 小时前' },
]
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div class="ds-row ds-wrap ds-gap-3" style="justify-content: space-between">
      <div><h2 class="ds-h2">部署 Deployments</h2><div class="ds-small">发布流水与版本</div></div>
      <DsButton icon="rocket">新建部署</DsButton>
    </div>

    <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
      <div v-for="d in deploys" :key="d.name" class="ds-row ds-gap-4" style="justify-content: space-between">
        <div class="ds-row ds-gap-3">
          <Icon name="box" :size="18" />
          <div class="ds-stack" style="gap: 2px">
            <span class="ds-h4" style="font-size: var(--ds-fs-body)">{{ d.name }}</span>
            <span class="ds-micro ds-mono">{{ d.ver }} · {{ d.time }}</span>
          </div>
        </div>
        <StatusBadge :status="d.status" />
      </div>
    </div>
  </div>
</template>
