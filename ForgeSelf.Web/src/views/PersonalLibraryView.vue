<template>
  <div class="personal-library-view">
    <PageHead
      eyebrow="Personal Library · 个人工具库"
      title="使用情况 · 能力成长"
    >
      <template #actions>
        <div class="time-range-selector">
          <button
            v-for="range in timeRanges"
            :key="range.value"
            class="range-btn"
            :class="{ active: currentTimeRange === range.value }"
            @click="changeTimeRange(range.value)"
          >
            {{ range.label }}
          </button>
        </div>
        <button class="refresh-btn" :disabled="isLoading" @click="loadAllData">
          <i class="fa-solid fa-rotate-right" :class="{ 'fa-spin': isLoading }" />
          刷新
        </button>
      </template>
    </PageHead>

    <div v-if="hasError" class="error-state">
      <EmptyState
        icon=""
        title="加载数据失败"
        sub="请检查后端服务后重试"
      >
        <template #action>
          <button class="retry-btn" @click="loadAllData">重新加载</button>
        </template>
      </EmptyState>
    </div>

    <div v-else class="view-content">
      <div class="stats-grid">
        <ForgeCard v-for="card in statCards" :key="card.key" class="stat-card">
          <div class="stat-icon" :class="card.iconClass">
            <i :class="card.icon" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ card.value }}</div>
            <div class="stat-label">{{ card.label }}</div>
          </div>
        </ForgeCard>
      </div>

      <ForgeCard class="chart-section" :padding="16">
        <div class="section-header">
          <h3><i class="fa-solid fa-chart-line" /> 能力成长曲线</h3>
        </div>
        <div ref="chartContainer" class="chart-container">
          <canvas ref="growthChartCanvas" />
          <div v-if="isLoadingGrowth" class="chart-loading">
            <i class="fa-solid fa-spinner fa-spin" />
          </div>
        </div>
      </ForgeCard>

      <div class="rankings-grid">
        <ForgeCard :padding="0" class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-toolbox" /> 常用工具 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in (stats.topTools || []).slice(0, 10)"
              :key="item.toolId"
              class="ranking-item"
            >
              <div class="rank-number" :class="'rank-' + (index + 1)">{{ index + 1 }}</div>
              <div class="rank-info">
                <div class="rank-name">{{ item.toolId }}</div>
                <div class="rank-subtitle">{{ item.pluginId }}</div>
              </div>
              <div class="rank-value">{{ item.useCount }} 次</div>
            </div>
            <EmptyState
              v-if="!stats.topTools?.length"
              icon="wrench"
              title="暂无工具使用记录"
              sub="暂无数据"
            />
          </div>
        </ForgeCard>

        <ForgeCard :padding="0" class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-file-code" /> 常用脚本 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in (stats.topScripts || []).slice(0, 10)"
              :key="item.scriptId"
              class="ranking-item"
            >
              <div class="rank-number" :class="'rank-' + (index + 1)">{{ index + 1 }}</div>
              <div class="rank-info">
                <div class="rank-name">{{ item.scriptName }}</div>
                <div class="rank-subtitle">{{ item.language }}</div>
              </div>
              <div class="rank-value">{{ item.usageCount }} 次</div>
            </div>
            <EmptyState
              v-if="!stats.topScripts?.length"
              icon="code"
              title="暂无脚本使用记录"
              sub="暂无数据"
            />
          </div>
        </ForgeCard>

        <ForgeCard :padding="0" class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-diagram-project" /> 常用工作流 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in (stats.topWorkflows || []).slice(0, 10)"
              :key="item.workflowId"
              class="ranking-item"
            >
              <div class="rank-number" :class="'rank-' + (index + 1)">{{ index + 1 }}</div>
              <div class="rank-info">
                <div class="rank-name">{{ item.workflowName }}</div>
                <div class="rank-subtitle">成功率: {{ (item.successRate * 100).toFixed(0) }}%</div>
              </div>
              <div class="rank-value">{{ item.executionCount }} 次</div>
            </div>
            <EmptyState
              v-if="!stats.topWorkflows?.length"
              icon="network"
              title="暂无工作流使用记录"
              sub="暂无数据"
            />
          </div>
        </ForgeCard>
      </div>

      <ForgeCard class="time-saved-section" :padding="0">
        <div class="section-header">
          <h3><i class="fa-solid fa-hourglass-half" /> 节省时间估算</h3>
        </div>
        <div class="time-saved-content">
          <div class="time-saved-main">
            <div class="time-saved-value">
              {{ timeSaved.totalTimeSavedHours?.toFixed(1) || 0 }}
              <span class="unit">小时</span>
            </div>
            <div class="time-saved-label">累计节省时间</div>
          </div>
          <div v-if="timeSaved.breakdown?.length" class="time-saved-breakdown">
            <div
              v-for="item in timeSaved.breakdown"
              :key="item.category"
              class="breakdown-item"
            >
              <span class="breakdown-category">{{ item.category }}</span>
              <span class="breakdown-value">{{ item.timeSavedMinutes.toFixed(0) }} 分钟</span>
            </div>
          </div>
        </div>
      </ForgeCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import type {
  PersonalLibraryStatsDto,
  GrowthCurvePoint,
  TimeSavedEstimateDto
} from '@/types/usageStats'
import { usageStatsApi } from '@/services/usageStatsApi'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'

const timeRanges = [
  { label: '7天', value: '7d' },
  { label: '30天', value: '30d' },
  { label: '90天', value: '90d' },
  { label: '全部', value: 'all' }
]

const currentTimeRange = ref('30d')
const isLoading = ref(false)
const isLoadingGrowth = ref(false)
const hasError = ref(false)

const stats = ref<PersonalLibraryStatsDto>({
  scriptCount: 0,
  codeSnippetCount: 0,
  workflowCount: 0,
  favoriteCount: 0,
  totalUsageCount: 0,
  totalUsageDurationSeconds: 0,
  timeRange: '30d',
  topTools: [],
  topScripts: [],
  topWorkflows: [],
  recentUsage: []
})

const growthCurve = ref<GrowthCurvePoint[]>([])
const timeSaved = ref<TimeSavedEstimateDto>({
  totalTimeSavedMinutes: 0,
  totalTimeSavedHours: 0,
  averageTimeSavedPerUse: 0,
  breakdown: []
})

const growthChartCanvas = ref<HTMLCanvasElement | null>(null)
const chartContainer = ref<HTMLDivElement | null>(null)

interface StatCard {
  key: string
  icon: string
  iconClass: string
  value: string | number
  label: string
}

const statCards = computed<StatCard[]>(() => [
  { key: 'scripts', icon: 'fa-solid fa-file-code', iconClass: 'icon-script', value: stats.value.scriptCount, label: '脚本数量' },
  { key: 'snippets', icon: 'fa-solid fa-code', iconClass: 'icon-snippet', value: stats.value.codeSnippetCount, label: '代码片段' },
  { key: 'workflows', icon: 'fa-solid fa-diagram-project', iconClass: 'icon-workflow', value: stats.value.workflowCount, label: '工作流数量' },
  { key: 'favorites', icon: 'fa-solid fa-star', iconClass: 'icon-favorite', value: stats.value.favoriteCount, label: '收藏数量' },
  { key: 'usage', icon: 'fa-solid fa-play-circle', iconClass: 'icon-usage', value: stats.value.totalUsageCount, label: '总使用次数' },
  { key: 'time', icon: 'fa-solid fa-clock', iconClass: 'icon-time', value: formatDuration(stats.value.totalUsageDurationSeconds), label: '总使用时长' }
])

function formatDuration(seconds: number): string {
  if (!seconds) return '0秒'
  if (seconds < 60) return `${seconds.toFixed(0)}秒`
  if (seconds < 3600) return `${(seconds / 60).toFixed(1)}分钟`
  return `${(seconds / 3600).toFixed(1)}小时`
}

async function loadStats() {
  try {
    hasError.value = false
    stats.value = await usageStatsApi.getPersonalLibraryStats(currentTimeRange.value)
  } catch (e) {
    console.error('加载个人统计失败:', e)
    hasError.value = true
  }
}

async function loadGrowthCurve() {
  try {
    isLoadingGrowth.value = true
    const days = currentTimeRange.value === 'all' ? 365 :
                 currentTimeRange.value === '90d' ? 90 :
                 currentTimeRange.value === '30d' ? 30 : 7
    growthCurve.value = await usageStatsApi.getGrowthCurve(days)
    drawGrowthChart()
  } catch (e) {
    console.error('加载成长曲线失败:', e)
  } finally {
    isLoadingGrowth.value = false
  }
}

async function loadTimeSaved() {
  try {
    timeSaved.value = await usageStatsApi.getTimeSavedEstimate()
  } catch (e) {
    console.error('加载节省时间失败:', e)
  }
}

async function loadAllData() {
  isLoading.value = true
  try {
    await Promise.all([
      loadStats(),
      loadGrowthCurve(),
      loadTimeSaved()
    ])
  } finally {
    isLoading.value = false
  }
}

function changeTimeRange(range: string) {
  currentTimeRange.value = range
  loadAllData()
}

/** 从主题 token 读取画布配色（DESIGN.md：canvas 不硬编码色值，取色用 getComputedStyle） */
function readThemeColors() {
  const root = getComputedStyle(document.documentElement)
  return {
    gold: root.getPropertyValue('--gold-400').trim() || '#F59E0B',
    fgTertiary: root.getPropertyValue('--fg-tertiary').trim() || '#8F8478',
    fgPrimary: root.getPropertyValue('--fg-primary').trim() || '#F4F0E7'
  }
}

function drawGrowthChart() {
  const canvas = growthChartCanvas.value
  const container = chartContainer.value
  if (!canvas || !container || growthCurve.value.length === 0) return

  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dpr = window.devicePixelRatio || 1
  const rect = container.getBoundingClientRect()
  const width = rect.width
  const height = 250

  canvas.width = width * dpr
  canvas.height = height * dpr
  canvas.style.width = width + 'px'
  canvas.style.height = height + 'px'
  ctx.scale(dpr, dpr)

  const { gold, fgTertiary, fgPrimary } = readThemeColors()

  const padding = { top: 20, right: 20, bottom: 40, left: 50 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom

  ctx.clearRect(0, 0, width, height)

  const data = growthCurve.value
  if (data.length === 0) return

  const maxValue = Math.max(...data.map(d => d.usageCount), 1)

  // 网格线：用 fgPrimary 的低透明派生
  ctx.strokeStyle = fgPrimary.replace(')', ', 0.08)').replace('rgb', 'rgba')
  if (!ctx.strokeStyle.startsWith('rgba')) {
    // hex 转 rgba 近似：直接用 color-mix 不行（canvas），退而用半透明黑/白
    ctx.strokeStyle = 'rgba(245, 240, 231, 0.08)'
  }
  ctx.lineWidth = 1
  for (let i = 0; i <= 4; i++) {
    const y = padding.top + (chartHeight / 4) * i
    ctx.beginPath()
    ctx.moveTo(padding.left, y)
    ctx.lineTo(width - padding.right, y)
    ctx.stroke()
  }

  ctx.fillStyle = fgTertiary
  ctx.font = '11px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
  ctx.textAlign = 'right'
  ctx.textBaseline = 'middle'
  for (let i = 0; i <= 4; i++) {
    const y = padding.top + (chartHeight / 4) * i
    const value = Math.round(maxValue * (1 - i / 4))
    ctx.fillText(String(value), padding.left - 8, y)
  }

  const stepX = chartWidth / Math.max(data.length - 1, 1)

  // 面积渐变：金 30% → 金 2%
  const usageGradient = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartHeight)
  usageGradient.addColorStop(0, gold + '4D')  // 30% alpha
  usageGradient.addColorStop(1, gold + '05')  // ~2% alpha

  ctx.beginPath()
  ctx.moveTo(padding.left, padding.top + chartHeight)
  data.forEach((point, i) => {
    const x = padding.left + i * stepX
    const y = padding.top + chartHeight - (point.usageCount / maxValue) * chartHeight
    ctx.lineTo(x, y)
  })
  ctx.lineTo(padding.left + (data.length - 1) * stepX, padding.top + chartHeight)
  ctx.closePath()
  ctx.fillStyle = usageGradient
  ctx.fill()

  ctx.beginPath()
  ctx.strokeStyle = gold
  ctx.lineWidth = 2
  data.forEach((point, i) => {
    const x = padding.left + i * stepX
    const y = padding.top + chartHeight - (point.usageCount / maxValue) * chartHeight
    if (i === 0) {
      ctx.moveTo(x, y)
    } else {
      ctx.lineTo(x, y)
    }
  })
  ctx.stroke()

  data.forEach((point, i) => {
    const x = padding.left + i * stepX
    const y = padding.top + chartHeight - (point.usageCount / maxValue) * chartHeight
    ctx.beginPath()
    ctx.arc(x, y, 3, 0, Math.PI * 2)
    ctx.fillStyle = gold
    ctx.fill()
  })

  ctx.fillStyle = fgTertiary
  ctx.font = '10px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'top'
  const labelStep = Math.ceil(data.length / 7)
  data.forEach((point, i) => {
    if (i % labelStep === 0 || i === data.length - 1) {
      const x = padding.left + i * stepX
      const dateStr = point.date.substring(5)
      ctx.fillText(dateStr, x, padding.top + chartHeight + 8)
    }
  })

  const legendY = padding.top + chartHeight + 28
  ctx.fillStyle = gold
  ctx.fillRect(padding.left, legendY, 12, 3)
  ctx.fillStyle = fgTertiary
  ctx.textAlign = 'left'
  ctx.font = '11px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
  ctx.fillText('使用次数', padding.left + 18, legendY - 1)
}

let resizeObserver: ResizeObserver | null = null

onMounted(async () => {
  await loadAllData()
  await nextTick()
  drawGrowthChart()

  if (chartContainer.value && typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(() => {
      drawGrowthChart()
    })
    resizeObserver.observe(chartContainer.value)
  }
})

watch(() => growthCurve.value, () => {
  nextTick(() => {
    drawGrowthChart()
  })
})
</script>

<style scoped>
.personal-library-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow-y: auto;
  padding: 20px 24px 36px;
}

.time-range-selector {
  display: flex;
  gap: 4px;
  padding: 3px;
  background: var(--paper-tint-weak);
  border-radius: var(--radius-md);
}

.range-btn {
  padding: 6px 14px;
  border: none;
  background: transparent;
  color: var(--fg-tertiary);
  font-size: 12px;
  cursor: pointer;
  border-radius: var(--radius-sm);
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.range-btn:hover {
  color: var(--fg-primary);
}

.range-btn.active {
  background: var(--gold-tint-strong);
  color: var(--gold-400);
}

.refresh-btn {
  padding: 8px 14px;
  border: 1px solid var(--border-default);
  background: transparent;
  color: var(--fg-secondary);
  border-radius: var(--radius-md);
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.refresh-btn:hover:not(:disabled) {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.refresh-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.view-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 16px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 14px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: var(--gold-400);
  background: var(--gold-tint);
  flex-shrink: 0;
}

.stat-value {
  font-family: var(--font-mono);
  font-size: 24px;
  font-weight: 700;
  color: var(--fg-primary);
  line-height: 1.2;
}

.stat-label {
  font-size: 12px;
  color: var(--fg-secondary);
  margin-top: 2px;
}

.section-header {
  padding: 14px 18px;
  border-bottom: 1px solid var(--border-hairline);
}

.section-header h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-header h3 i {
  color: var(--gold-400);
  font-size: 13px;
}

.chart-container {
  position: relative;
  padding: 16px;
  min-height: 250px;
}

.chart-loading {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: color-mix(in srgb, var(--ink-950) 60%, transparent);
  color: var(--fg-tertiary);
}

.rankings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 16px;
}

.ranking-list {
  max-height: 380px;
  overflow-y: auto;
}

.ranking-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 16px;
  border-bottom: 1px solid var(--border-hairline);
}

.ranking-item:last-child {
  border-bottom: none;
}

.rank-number {
  width: 24px;
  height: 24px;
  border-radius: var(--radius-full, 9999px);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  font-family: var(--font-mono);
  background: var(--paper-tint);
  color: var(--fg-tertiary);
  flex-shrink: 0;
}

.rank-1 { background: var(--gold-tint-strong); color: var(--gold-400); }
.rank-2 { background: var(--paper-tint); color: var(--fg-secondary); }
.rank-3 { background: var(--gold-tint); color: var(--gold-300); }

.rank-info {
  flex: 1;
  min-width: 0;
}

.rank-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rank-subtitle {
  font-size: 11px;
  color: var(--fg-tertiary);
  margin-top: 2px;
  font-family: var(--font-mono);
}

.rank-value {
  font-size: 12px;
  font-weight: 500;
  color: var(--gold-400);
  font-family: var(--font-mono);
  flex-shrink: 0;
}

.time-saved-content {
  display: flex;
  align-items: center;
  gap: 30px;
  padding: 24px;
  flex-wrap: wrap;
}

.time-saved-main {
  text-align: center;
  min-width: 180px;
}

.time-saved-value {
  font-family: var(--font-mono);
  font-size: 36px;
  font-weight: 700;
  color: var(--gold-400);
  line-height: 1;
}

.time-saved-value .unit {
  font-size: 16px;
  font-weight: 500;
  margin-left: 4px;
}

.time-saved-label {
  font-size: 13px;
  color: var(--fg-secondary);
  margin-top: 6px;
}

.time-saved-breakdown {
  flex: 1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px;
}

.breakdown-item {
  display: flex;
  justify-content: space-between;
  padding: 10px 12px;
  background: var(--paper-tint-weak);
  border-radius: var(--radius-md);
  font-size: 12px;
}

.breakdown-category {
  color: var(--fg-secondary);
}

.breakdown-value {
  font-weight: 500;
  color: var(--fg-primary);
  font-family: var(--font-mono);
}

.error-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.retry-btn {
  padding: 8px 20px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-md);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: box-shadow var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
}

.retry-btn:hover {
  box-shadow: var(--shadow-brand);
  transform: translateY(-1px);
}

.retry-btn:active {
  transform: translateY(1px);
}
</style>
