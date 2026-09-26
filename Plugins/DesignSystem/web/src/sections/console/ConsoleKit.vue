<script setup lang="ts">
/**
 * 控制台 UI Kit（Console UI Kit）· 控制中枢。
 * 五大分区：Overview / Services / Topology / Config / Deployments。
 * 复用的设计系统组件：NavItem / MetricCard / StatusBadge / TableRow / TopologyMotif / Icon。
 */
import { ref } from 'vue'
import NavItem from '../../components/NavItem.vue'
import Icon from '../../components/Icon.vue'
import ConsoleOverview from './ConsoleOverview.vue'
import ConsoleServices from './ConsoleServices.vue'
import ConsoleTopology from './ConsoleTopology.vue'
import ConsoleConfig from './ConsoleConfig.vue'
import ConsoleDeployments from './ConsoleDeployments.vue'

const pages = [
  { key: 'overview', label: 'Overview', icon: 'dashboard' },
  { key: 'services', label: 'Services', icon: 'server' },
  { key: 'topology', label: 'Topology', icon: 'network' },
  { key: 'config', label: 'Config', icon: 'settings' },
  { key: 'deployments', label: 'Deployments', icon: 'rocket' },
] as const

const active = ref<(typeof pages)[number]['key']>('overview')
</script>

<template>
  <div class="ds-console">
    <aside class="ds-console__side">
      <div class="ds-console__brand">
        <Icon name="box" :size="18" />
        <span class="ds-h4">ForgeSelf 控制台</span>
      </div>
      <nav class="ds-stack ds-gap-1">
        <NavItem
          v-for="p in pages"
          :key="p.key"
          :label="p.label"
          :icon="p.icon"
          :active="active === p.key"
          @click="active = p.key"
        />
      </nav>
    </aside>
    <div class="ds-console__main">
      <ConsoleOverview v-if="active === 'overview'" />
      <ConsoleServices v-else-if="active === 'services'" />
      <ConsoleTopology v-else-if="active === 'topology'" />
      <ConsoleConfig v-else-if="active === 'config'" />
      <ConsoleDeployments v-else-if="active === 'deployments'" />
    </div>
  </div>
</template>

<style scoped>
.ds-console {
  display: grid;
  grid-template-columns: 240px 1fr;
  gap: var(--ds-space-6);
  align-items: start;
}
.ds-console__side {
  position: sticky;
  top: 96px;
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  padding: var(--ds-space-3);
  box-shadow: var(--ds-shadow-sm);
}
.ds-console__brand {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  padding: var(--ds-space-3) var(--ds-space-3) var(--ds-space-4);
  color: var(--ds-color-primary);
  border-bottom: 1px solid var(--ds-border-1);
  margin-bottom: var(--ds-space-2);
}
.ds-console__main {
  min-width: 0;
}
@media (max-width: 760px) {
  .ds-console {
    grid-template-columns: 1fr;
  }
  .ds-console__side {
    position: static;
  }
}
</style>
