<script setup lang="ts">
/**
 * 组件库展示（Component Gallery）。
 * Buttons(4态) / Inputs / Status Badges / Metric Cards / Code Block / Nav Item / Table Row / Brand / Icon / Topology Motif。
 */
import BrandLogo from '../components/BrandLogo.vue'
import Icon from '../components/Icon.vue'
import StatusBadge from '../components/StatusBadge.vue'
import MetricCard from '../components/MetricCard.vue'
import DsButton from '../components/DsButton.vue'
import DsInput from '../components/DsInput.vue'
import CodeBlock from '../components/CodeBlock.vue'
import NavItem from '../components/NavItem.vue'
import TableRow from '../components/TableRow.vue'
import TopologyMotif from '../components/TopologyMotif.vue'

const variants = ['primary', 'secondary', 'ghost', 'danger'] as const
const iconNames = ['dashboard', 'server', 'network', 'settings', 'rocket', 'activity', 'gauge', 'box', 'cpu', 'link', 'check', 'alert-triangle']

const yaml = `# design-system.yaml · 设计系统主题配置（示例，随当前系统换肤）
theme:
  brand:
    primary: var(--ds-brand-600)
    accent: var(--ds-accent-500)
  tokens:
    spacing_base: 8         # 8pt 基准
    radius_pill: 999
  surfaces:
    - surface-1
    - surface-2
    - surface-3
  semantic:
    success: true
    warning: true
    danger: false
`

const metrics = [
  { label: '服务总数', value: 128, delta: '+6', trend: 'up' as const, hint: '近 7 天' },
  { label: '集群负载', value: 62.4, unit: '%', delta: '-3.1%', trend: 'down' as const, hint: 'P99 健康' },
  { label: '在线节点', value: 42, delta: '0', trend: 'flat' as const },
  { label: '请求 / 分', value: 18.2, unit: 'k', delta: '+1.2k', trend: 'up' as const },
]

const navItems = [
  { label: 'Overview', icon: 'dashboard', active: true },
  { label: 'Services', icon: 'server', active: false },
  { label: 'Topology', icon: 'network', active: false },
  { label: 'Config', icon: 'settings', active: false },
  { label: 'Deployments', icon: 'rocket', active: false },
]

const rows = [
  { name: 'api-gateway', region: 'cn-east-1', status: 'healthy' as const, p99: '42ms', requests: '9.4k/s' },
  { name: 'auth-service', region: 'cn-east-1', status: 'degraded' as const, p99: '118ms', requests: '3.1k/s' },
  { name: 'worker-pool', region: 'us-west-2', status: 'deploying' as const, p99: '—', requests: '—' },
  { name: 'billing-svc', region: 'eu-central', status: 'down' as const, p99: '—', requests: '0' },
]
</script>

<template>
  <div class="ds-stack ds-gap-6">
    <!-- Buttons -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">A</span><h2 class="ds-h2">Buttons · Primary / Secondary / Ghost / Danger</h2></div>
      <div class="ds-surface ds-stack ds-gap-5" style="padding: var(--ds-space-6)">
        <div class="ds-row ds-wrap ds-gap-3">
          <DsButton v-for="v in variants" :key="v" :variant="v">{{ v }}</DsButton>
        </div>
        <div class="ds-row ds-wrap ds-gap-3">
          <DsButton v-for="v in variants" :key="v + 'i'" :variant="v" icon="rocket">带图标</DsButton>
        </div>
        <div class="ds-row ds-wrap ds-gap-3">
          <DsButton v-for="v in variants" :key="v + 'd'" :variant="v" disabled>Disabled</DsButton>
        </div>
        <div class="ds-small">状态：default → hover → active → focus(键盘 Tab 可见青色描边) → disabled。</div>
      </div>
    </section>

    <!-- Inputs -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">B</span><h2 class="ds-h2">Inputs · default + focus</h2></div>
      <div class="ds-surface ds-row ds-wrap ds-gap-5" style="padding: var(--ds-space-6)">
        <DsInput label="服务名称" placeholder="my-service" style="min-width: 240px" />
        <DsInput label="含错误" placeholder="invalid" invalid hint="名称已存在" style="min-width: 240px" />
      </div>
    </section>

    <!-- Status Badges -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">C</span><h2 class="ds-h2">Status Badges · 健康 / 降级 / 故障 / 部署中</h2></div>
      <div class="ds-surface ds-row ds-wrap ds-gap-3" style="padding: var(--ds-space-6)">
        <StatusBadge status="healthy" />
        <StatusBadge status="degraded" />
        <StatusBadge status="down" />
        <StatusBadge status="deploying" />
      </div>
    </section>

    <!-- Metric Cards -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">D</span><h2 class="ds-h2">Metric Cards</h2></div>
      <div class="ds-swatch-grid">
        <MetricCard v-for="m in metrics" :key="m.label" v-bind="m" />
      </div>
    </section>

    <!-- Code Block -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">E</span><h2 class="ds-h2">Code Block · YAML 高亮</h2></div>
      <CodeBlock :code="yaml" filename="design-system.yaml" />
    </section>

    <!-- Nav Item + Table Row -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">F</span><h2 class="ds-h2">Nav Item &amp; Table Row</h2></div>
      <div class="ds-row ds-wrap ds-gap-6">
        <div class="ds-surface" style="padding: var(--ds-space-3); min-width: 220px">
          <NavItem v-for="n in navItems" :key="n.label" :label="n.label" :icon="n.icon" :active="n.active" />
        </div>
        <div class="ds-surface" style="flex: 1; min-width: 360px; padding: 0; overflow: hidden">
          <div class="ds-row-item" style="background: var(--ds-surface-2); font-weight: var(--ds-fw-semibold); color: var(--ds-fg-3)">
            <div>服务</div><div>状态</div><div>P99</div><div>吞吐</div>
          </div>
          <TableRow v-for="r in rows" :key="r.name" v-bind="r" />
        </div>
      </div>
    </section>

    <!-- Brand / Icon / Topology -->
    <section>
      <div class="ds-section-title"><span class="ds-eyebrow">G</span><h2 class="ds-h2">Brand · Logo / Icon System / Topology Motif</h2></div>
      <div class="ds-surface ds-row ds-wrap ds-gap-6" style="padding: var(--ds-space-6); align-items: center">
        <div class="ds-stack ds-gap-3">
          <BrandLogo variant="full" :size="36" />
          <BrandLogo variant="mark" :size="32" />
          <BrandLogo variant="wordmark" :size="28" />
        </div>
        <div class="ds-stack ds-gap-3">
          <div class="ds-micro">Icon System · Lucide · 1.5px stroke</div>
          <div class="ds-row ds-wrap ds-gap-3">
            <span v-for="i in iconNames" :key="i" class="ds-icon-tile"><Icon :name="i" :size="20" /></span>
          </div>
        </div>
        <TopologyMotif :size="160" />
      </div>
    </section>
  </div>
</template>

<style scoped>
.ds-icon-tile {
  width: 40px;
  height: 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  color: var(--ds-color-primary);
  background: var(--ds-surface-1);
}
</style>
