<template>
  <div class="personal-library-view">
    <div class="view-header">
      <div class="header-left">
        <h2>
          <i class="fa-solid fa-gauge-high" />
          个人工具库
        </h2>
        <p class="view-subtitle">查看你的工具使用情况和能力成长曲线</p>
      </div>
      <div class="header-right">
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
      </div>
    </div>

    <div v-if="!hasError" class="view-content">
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon icon-script">
            <i class="fa-solid fa-file-code" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.scriptCount }}</div>
            <div class="stat-label">脚本数量</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon icon-snippet">
            <i class="fa-solid fa-code" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.codeSnippetCount }}</div>
            <div class="stat-label">代码片段</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon icon-workflow">
            <i class="fa-solid fa-diagram-project" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.workflowCount }}</div>
            <div class="stat-label">工作流数量</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon icon-favorite">
            <i class="fa-solid fa-star" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.favoriteCount }}</div>
            <div class="stat-label">收藏数量</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon icon-usage">
            <i class="fa-solid fa-play-circle" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.totalUsageCount }}</div>
            <div class="stat-label">总使用次数</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon icon-time">
            <i class="fa-solid fa-clock" />
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ formatDuration(stats.totalUsageDurationSeconds) }}</div>
            <div class="stat-label">总使用时长</div>
          </div>
        </div>
      </div>

      <div class="chart-section">
        <div class="section-header">
          <h3><i class="fa-solid fa-chart-line" /> 能力成长曲线</h3>
        </div>
        <div ref="chartContainer" class="chart-container">
          <canvas ref="growthChartCanvas" />
          <div v-if="isLoadingGrowth" class="chart-loading">
            <i class="fa-solid fa-spinner fa-spin" />
          </div>
        </div>
      </div>

      <div class="rankings-grid">
        <div class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-toolbox" /> 常用工具 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in stats.topTools?.slice(0, 10) || []"
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
            <div v-if="!stats.topTools?.length" class="empty-ranking">
              暂无数据
            </div>
          </div>
        </div>

        <div class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-file-code" /> 常用脚本 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in stats.topScripts?.slice(0, 10) || []"
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
            <div v-if="!stats.topScripts?.length" class="empty-ranking">
              暂无数据
            </div>
          </div>
        </div>

        <div class="ranking-card">
          <div class="section-header">
            <h3><i class="fa-solid fa-diagram-project" /> 常用工作流 Top 10</h3>
          </div>
          <div class="ranking-list">
            <div
              v-for="(item, index) in stats.topWorkflows?.slice(0, 10) || []"
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
            <div v-if="!stats.topWorkflows?.length" class="empty-ranking">
              暂无数据
            </div>
          </div>
        </div>
      </div>

      <div class="time-saved-section">
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
      </div>
    </div>

    <div v-else class="error-state">
      <i class="fa-solid fa-triangle-exclamation" />
      <p>加载数据失败</p>
      <button class="retry-btn" @click="loadAllData">重新加载</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, nextTick, watch } from 'vue'
import type {
  PersonalLibraryStatsDto,
  GrowthCurvePoint,
  TimeSavedEstimateDto
} from '@/types/usageStats'
import { usageStatsApi } from '@/services/usageStatsApi'

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

  const padding = { top: 20, right: 20, bottom: 40, left: 50 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom

  ctx.clearRect(0, 0, width, height)

  const data = growthCurve.value
  if (data.length === 0) return

  const maxValue = Math.max(...data.map(d => d.usageCount), 1)

  ctx.strokeStyle = 'rgba(0, 0, 0, 0.08)'
  ctx.lineWidth = 1
  for (let i = 0; i <= 4; i++) {
    const y = padding.top + (chartHeight / 4) * i
    ctx.beginPath()
    ctx.moveTo(padding.left, y)
    ctx.lineTo(width - padding.right, y)
    ctx.stroke()
  }

  ctx.fillStyle = '#6b7280'
  ctx.font = '11px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
  ctx.textAlign = 'right'
  ctx.textBaseline = 'middle'
  for (let i = 0; i <= 4; i++) {
    const y = padding.top + (chartHeight / 4) * i
    const value = Math.round(maxValue * (1 - i / 4))
    ctx.fillText(String(value), padding.left - 8, y)
  }

  const stepX = chartWidth / Math.max(data.length - 1, 1)

  const usageGradient = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartHeight)
  usageGradient.addColorStop(0, 'rgba(59, 130, 246, 0.3)')
  usageGradient.addColorStop(1, 'rgba(59, 130, 246, 0.02)')

  ctx.beginPath()
  ctx.moveTo(padding.left, padding.top + chartHeight)
  data.forEach((point, i) => {
    const x = padding.left + i * stepX
    const y = padding.top + chartHeight - (point.usageCount / maxValue) * chartHeight
    if (i === 0) {
      ctx.lineTo(x, y)
    } else {
      ctx.lineTo(x, y)
    }
  })
  ctx.lineTo(padding.left + (data.length - 1) * stepX, padding.top + chartHeight)
  ctx.closePath()
  ctx.fillStyle = usageGradient
  ctx.fill()

  ctx.beginPath()
  ctx.strokeStyle = '#3b82f6'
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
    ctx.fillStyle = '#3b82f6'
    ctx.fill()
  })

  ctx.fillStyle = '#6b7280'
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
  ctx.fillStyle = '#3b82f6'
  ctx.fillRect(padding.left, legendY, 12, 3)
  ctx.fillStyle = '#6b7280'
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
  background: var(--bg-primary, #f9fafb);
}

.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  background: var(--bg-card, #fff);
  border-bottom: 1px solid var(--border-color, #e5e7eb);
  flex-wrap: wrap;
  gap: 16px;
}

.header-left h2 {
  margin: 0 0 4px 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary, #1f2937);
  display: flex;
  align-items: center;
  gap: 10px;
}

.header-left h2 i {
  color: var(--primary-color, #3b82f6);
}

.view-subtitle {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary, #6b7280);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.time-range-selector {
  display: flex;
  background: var(--bg-secondary, #f3f4f6);
  border-radius: 8px;
  padding: 3px;
}

.range-btn {
  padding: 6px 14px;
  border: none;
  background: transparent;
  color: var(--text-secondary, #6b7280);
  font-size: 12px;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s;
}

.range-btn.active {
  background: var(--bg-card, #fff);
  color: var(--primary-color, #3b82f6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.refresh-btn {
  padding: 8px 14px;
  border: 1px solid var(--border-color, #d1d5db);
  background: var(--bg-card, #fff);
  color: var(--text-secondary, #6b7280);
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
}

.refresh-btn:hover:not(:disabled) {
  background: var(--bg-secondary, #f3f4f6);
}

.view-content {
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
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
  padding: 18px;
  background: var(--bg-card, #fff);
  border: 1px solid var(--border-color, #e5e7eb);
  border-radius: 10px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: white;
  flex-shrink: 0;
}

.icon-script { background: linear-gradient(135deg, #3b82f6, #2563eb); }
.icon-snippet { background: linear-gradient(135deg, #8b5cf6, #7c3aed); }
.icon-workflow { background: linear-gradient(135deg, #10b981, #059669); }
.icon-favorite { background: linear-gradient(135deg, #f59e0b, #d97706); }
.icon-usage { background: linear-gradient(135deg, #ec4899, #db2777); }
.icon-time { background: linear-gradient(135deg, #06b6d4, #0891b2); }

.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary, #1f2937);
  line-height: 1.2;
}

.stat-label {
  font-size: 12px;
  color: var(--text-secondary, #6b7280);
  margin-top: 2px;
}

.chart-section,
.ranking-card,
.time-saved-section {
  background: var(--bg-card, #fff);
  border: 1px solid var(--border-color, #e5e7eb);
  border-radius: 10px;
  overflow: hidden;
}

.section-header {
  padding: 14px 18px;
  border-bottom: 1px solid var(--border-color, #e5e7eb);
  background: var(--bg-secondary, #f9fafb);
}

.section-header h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary, #1f2937);
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-header h3 i {
  color: var(--primary-color, #3b82f6);
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
  background: rgba(255, 255, 255, 0.8);
  color: var(--text-muted, #9ca3af);
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
  border-bottom: 1px solid var(--border-light, #f3f4f6);
}

.ranking-item:last-child {
  border-bottom: none;
}

.rank-number {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  background: var(--bg-secondary, #f3f4f6);
  color: var(--text-secondary, #6b7280);
  flex-shrink: 0;
}

.rank-1 { background: #fef3c7; color: #d97706; }
.rank-2 { background: #e5e7eb; color: #6b7280; }
.rank-3 { background: #fed7aa; color: #c2410c; }

.rank-info {
  flex: 1;
  min-width: 0;
}

.rank-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary, #1f2937);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rank-subtitle {
  font-size: 11px;
  color: var(--text-muted, #9ca3af);
  margin-top: 2px;
}

.rank-value {
  font-size: 12px;
  font-weight: 500;
  color: var(--primary-color, #3b82f6);
  flex-shrink: 0;
}

.empty-ranking {
  padding: 30px;
  text-align: center;
  color: var(--text-muted, #9ca3af);
  font-size: 13px;
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
  font-size: 36px;
  font-weight: 700;
  color: var(--primary-color, #3b82f6);
  line-height: 1;
}

.time-saved-value .unit {
  font-size: 16px;
  font-weight: 500;
  margin-left: 4px;
}

.time-saved-label {
  font-size: 13px;
  color: var(--text-secondary, #6b7280);
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
  background: var(--bg-secondary, #f9fafb);
  border-radius: 6px;
  font-size: 12px;
}

.breakdown-category {
  color: var(--text-secondary, #6b7280);
}

.breakdown-value {
  font-weight: 500;
  color: var(--text-primary, #1f2937);
}

.error-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--text-muted, #9ca3af);
  gap: 16px;
}

.error-state i {
  font-size: 48px;
  opacity: 0.3;
}

.error-state p {
  margin: 0;
  font-size: 14px;
}

.retry-btn {
  padding: 8px 20px;
  background: var(--primary-color, #3b82f6);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
}

.retry-btn:hover {
  background: var(--primary-hover, #2563eb);
}
</style>
