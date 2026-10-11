<script setup lang="ts">
/**
 * 成本观测主视图：四个页签（总览 / 单价 / 预算 / trace）。
 *
 * 设计原则（对齐 02-spec 信息架构）：
 * - **覆盖度与口径永远可见**：主聊天链路不可观测、关联为近似、未配单价不计总额——
 *   这些不是脚注，而是必须在界面上说清楚的事实（否则用户会把「部分成本」误读成「全部成本」）。
 */
import { onMounted, ref } from 'vue'
import DashboardPanel from './components/DashboardPanel.vue'
import PricePanel from './components/PricePanel.vue'
import BudgetPanel from './components/BudgetPanel.vue'
import TracePanel from './components/TracePanel.vue'
import { fetchPluginVersion } from './http'

type Tab = 'dashboard' | 'price' | 'budget' | 'trace'

const tabs: { key: Tab; label: string }[] = [
  { key: 'dashboard', label: '总览' },
  { key: 'price', label: '单价目录' },
  { key: 'budget', label: '预算' },
  { key: 'trace', label: 'Trace' },
]

const active = ref<Tab>('dashboard')

/** 自身版本号（铁律 13：插件 UI 必须展示版本号）。 */
const version = ref('')
onMounted(async () => {
  version.value = await fetchPluginVersion('cost-scope')
})
</script>

<template>
  <div class="cost-scope">
    <header class="cost-scope__head">
      <h2>成本观测 <span v-if="version" class="cs-version">v{{ version }}</span></h2>
      <nav class="cost-scope__tabs">
        <button
          v-for="t in tabs"
          :key="t.key"
          :class="{ 'is-active': active === t.key }"
          type="button"
          @click="active = t.key"
        >
          {{ t.label }}
        </button>
      </nav>
    </header>

    <main class="cost-scope__body">
      <DashboardPanel v-if="active === 'dashboard'" />
      <PricePanel v-else-if="active === 'price'" />
      <BudgetPanel v-else-if="active === 'budget'" />
      <TracePanel v-else />
    </main>
  </div>
</template>

<style scoped>
.cost-scope {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}
.cost-scope__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--el-border-color);
  padding-bottom: 8px;
}
.cs-version {
  font-size: 12px;
  font-weight: 500;
  padding: 1px 8px;
  border-radius: 10px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
  vertical-align: middle;
}
.cost-scope__tabs button {
  border: 1px solid var(--el-border-color);
  background: transparent;
  border-radius: 4px;
  padding: 4px 12px;
  margin-left: 8px;
  cursor: pointer;
}
.cost-scope__tabs button.is-active {
  font-weight: 600;
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
</style>
