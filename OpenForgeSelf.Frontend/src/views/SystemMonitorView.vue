<script setup lang="ts">
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { useSystemMonitorStore } from '@/stores/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'

const store = useSystemMonitorStore()
const processKeyword = ref('')
const sortField = ref<'cpuUsage' | 'memoryUsage' | 'name' | 'pid'>('cpuUsage')
const sortDir = ref<'asc' | 'desc'>('desc')

const filteredProcesses = computed(() => {
  let list = [...(store.processes ?? [])]
  if (processKeyword.value) {
    const kw = processKeyword.value.toLowerCase()
    list = list.filter(p => p.name.toLowerCase().includes(kw))
  }
  list.sort((a, b) => {
    const dir = sortDir.value === 'desc' ? -1 : 1
    if (sortField.value === 'name') return a.name.localeCompare(b.name) * dir
    return (a[sortField.value] - b[sortField.value]) * dir
  })
  return list
})

function toggleSort(field: 'cpuUsage' | 'memoryUsage' | 'name' | 'pid') {
  if (sortField.value === field) {
    sortDir.value = sortDir.value === 'desc' ? 'asc' : 'desc'
  } else {
    sortField.value = field
    sortDir.value = 'desc'
  }
}

function sortIcon(field: string) {
  if (sortField.value !== field) return '↕'
  return sortDir.value === 'desc' ? '↓' : '↑'
}

// CPU gauge conic gradient style
function cpuGaugeStyle(pct: number) {
  const deg = (pct / 100) * 360
  return { background: `conic-gradient(var(--primary-color) ${deg}deg, var(--bg-muted) ${deg}deg)` }
}

// Memory breakdown (approximate from overview)
const memoryBreakdown = computed(() => {
  const mem = store.overview?.memory
  if (!mem) return { system: 0, app: 0, cache: 0 }
  const systemBytes = mem.used * 0.5
  const appBytes = mem.used * 0.3
  const cacheBytes = mem.available
  return { system: systemBytes, app: appBytes, cache: cacheBytes }
})

const cpuHistoryLast12 = computed(() => {
  return store.cpuHistory.slice(-12)
})

function formatLastUpdate(timestamp: number): string {
  if (!timestamp) return '--'
  return new Date(timestamp).toLocaleTimeString('zh-CN')
}

async function handleKillProcess(pid: number): Promise<void> {
  await store.killProcess(pid)
}

onMounted(() => {
  store.connect()
  store.loadProcesses()
})

onUnmounted(() => {
  store.disconnect()
})
</script>

<template>
  <div class="system-monitor-view">
    <!-- Ambient glow -->
    <div class="ambient-glow" aria-hidden="true" />

    <div class="page-content">
      <!-- ===== PAGE HEADER ===== -->
      <div class="page-header">
        <div class="header-left">
          <svg
            class="header-icon"
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
          </svg>
          <h1 class="page-title">系统监控</h1>
          <span class="live-badge">
            <span class="live-dot" />
            实时
          </span>
          <span class="refresh-hint">数据每 2 秒刷新</span>
        </div>
        <div class="header-right">
          <div class="connection-status" :class="{ connected: store.isConnected }">
            <span class="status-dot" />
            <span class="status-text">{{ store.isConnected ? '已连接' : '已断开' }}</span>
          </div>
          <span class="update-time">{{ formatLastUpdate(store.lastUpdateTime) }}</span>
        </div>
      </div>

      <!-- Error banner -->
      <div v-if="store.error" class="error-banner" role="alert">
        <span class="error-msg">{{ store.error }}</span>
        <button class="error-close" @click="store.clearError()">✕</button>
      </div>

      <!-- ===== TOP STATS ROW (4 cards) ===== -->
      <div class="stats-grid">
        <!-- CPU -->
        <div class="stat-card">
          <div class="stat-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect
                x="4"
                y="4"
                width="16"
                height="16"
                rx="2"
              />
              <rect x="9" y="9" width="6" height="6" />
              <path d="M15 2v2M15 20v2M2 15h2M2 9h2M20 15h2M20 9h2M9 2v2M9 20v2" />
            </svg>
            <span class="stat-label">CPU 使用率</span>
          </div>
          <div class="stat-body">
            <div class="cpu-gauge" :style="cpuGaugeStyle(store.overview?.cpu.totalUsage || 0)">
              <span class="cpu-gauge-value">{{ (store.overview?.cpu.totalUsage || 0).toFixed(0) }}%</span>
            </div>
            <div class="stat-detail">
              <div class="stat-value">{{ (store.overview?.cpu.totalUsage || 0).toFixed(1) }}%</div>
              <div class="stat-sub">{{ store.overview?.cpu.perCoreUsage.length || '--' }} 核</div>
            </div>
          </div>
        </div>

        <!-- 内存 -->
        <div class="stat-card">
          <div class="stat-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect
                x="4"
                y="4"
                width="16"
                height="16"
                rx="2"
              />
              <path d="M9 4v16M15 4v16" />
            </svg>
            <span class="stat-label">内存</span>
          </div>
          <div class="stat-body-col">
            <div class="stat-value-row">
              <span class="stat-big">{{ store.overview?.memory ? systemMonitorApi.formatBytes(store.overview.memory.used, 1) : '--' }}</span>
              <span class="stat-divider">/</span>
              <span class="stat-dim">{{ store.overview?.memory ? systemMonitorApi.formatBytes(store.overview.memory.total, 1) : '--' }}</span>
            </div>
            <div class="h-bar-track">
              <div class="h-bar-fill" :style="{ width: (store.overview?.memory.usagePercent || 0) + '%', background: 'var(--info-color)' }" />
            </div>
            <div class="h-bar-labels">
              <span>{{ (store.overview?.memory.usagePercent || 0).toFixed(0) }}% 已用</span>
              <span style="color: var(--info-color);">{{ store.overview?.memory ? systemMonitorApi.formatBytes(store.overview.memory.available, 1) : '--' }} 可用</span>
            </div>
          </div>
        </div>

        <!-- 磁盘 -->
        <div class="stat-card">
          <div class="stat-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="22" y1="12" x2="2" y2="12" />
              <path d="M5.45 5.11L2 12v6a2 2 0 002 2h16a2 2 0 002-2v-6l-3.45-6.89A2 2 0 0016.76 4H7.24a2 2 0 00-1.79 1.11z" />
              <line x1="6" y1="16" x2="6.01" y2="16" />
              <line x1="10" y1="16" x2="10.01" y2="16" />
            </svg>
            <span class="stat-label">磁盘</span>
          </div>
          <div class="stat-body-col">
            <div class="stat-value-row">
              <span class="stat-big">{{ store.overview?.disks ? systemMonitorApi.formatBytes(store.overview.disks.reduce((s, d) => s + d.usedSpace, 0), 1) : '--' }}</span>
              <span class="stat-divider">/</span>
              <span class="stat-dim">{{ store.overview?.disks ? systemMonitorApi.formatBytes(store.overview.disks.reduce((s, d) => s + d.totalSize, 0), 1) : '--' }}</span>
            </div>
            <div class="h-bar-track">
              <div class="h-bar-fill" :style="{ width: Math.min(100, (store.overview?.disks?.reduce((s, d) => s + d.usedSpace, 0) || 0) / Math.max(1, (store.overview?.disks?.reduce((s, d) => s + d.totalSize, 0) || 1)) * 100) + '%', background: 'var(--success-color)' }" />
            </div>
            <div class="h-bar-labels">
              <span>{{ (store.overview?.disks?.reduce((s, d) => s + d.usagePercent, 0) ?? 0) / Math.max(1, store.overview?.disks?.length ?? 1) }}% 已用</span>
              <span style="color: var(--success-color);">{{ store.overview?.disks ? systemMonitorApi.formatBytes(store.overview.disks.reduce((s, d) => s + d.freeSpace, 0), 1) : '--' }} 可用</span>
            </div>
          </div>
        </div>

        <!-- 网络 -->
        <div class="stat-card">
          <div class="stat-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M5 12.55a11 11 0 0114.08 0" />
              <path d="M1.42 9a16 16 0 0121.16 0" />
              <path d="M8.53 16.11a6 6 0 016.95 0" />
              <line x1="12" y1="20" x2="12.01" y2="20" />
            </svg>
            <span class="stat-label">网络</span>
          </div>
          <div class="stat-body-col">
            <div class="net-row">
              <span class="net-up-icon">
                <svg
                  width="12"
                  height="12"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2.5"
                ><line x1="12" y1="19" x2="12" y2="5" /><polyline points="5 12 12 5 19 12" /></svg>
              </span>
              <span class="net-up-value">{{ store.overview?.network ? systemMonitorApi.formatSpeed(store.overview.network.uploadSpeed) : '--' }}</span>
            </div>
            <div class="net-row">
              <span class="net-down-icon">
                <svg
                  width="12"
                  height="12"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2.5"
                ><line x1="12" y1="5" x2="12" y2="19" /><polyline points="19 12 12 19 5 12" /></svg>
              </span>
              <span class="net-down-value">{{ store.overview?.network ? systemMonitorApi.formatSpeed(store.overview.network.downloadSpeed) : '--' }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- ===== MIDDLE SECTION (2 columns) ===== -->
      <div class="middle-grid">
        <!-- CPU History Bar Chart -->
        <div class="section-card">
          <div class="section-card-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="18" y1="20" x2="18" y2="10" /><line x1="12" y1="20" x2="12" y2="4" /><line x1="6" y1="20" x2="6" y2="14" />
            </svg>
            <span class="section-card-title">CPU 历史</span>
            <span class="section-card-badge">最近 12 次</span>
          </div>
          <div class="bar-chart">
            <div
              v-for="(point, i) in cpuHistoryLast12"
              :key="i"
              class="bar"
              :class="{ 'bar-current': i === cpuHistoryLast12.length - 1 }"
              :style="{ height: Math.max(4, point.value) + '%' }"
              :title="point.value.toFixed(1) + '%'"
            />
          </div>
          <div class="bar-axis">
            <span>24s 前</span>
            <span style="color: var(--primary-color);">当前</span>
          </div>
        </div>

        <!-- Memory Breakdown Stacked Bar -->
        <div class="section-card">
          <div class="section-card-header">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M21.21 15.89A10 10 0 118 2.83" />
              <path d="M22 12A10 10 0 0012 2v10z" />
            </svg>
            <span class="section-card-title">内存占用分布</span>
          </div>
          <div class="stacked-bar-track">
            <div
              class="stacked-bar-seg"
              :style="{ width: (memoryBreakdown.system / Math.max(1, (store.overview?.memory.total || 1)) * 100) + '%', background: 'var(--info-color)' }"
            >
              {{ systemMonitorApi.formatBytes(memoryBreakdown.system, 1) }}
            </div>
            <div
              class="stacked-bar-seg"
              :style="{ width: (memoryBreakdown.app / Math.max(1, (store.overview?.memory.total || 1)) * 100) + '%', background: 'var(--primary-color)' }"
            >
              {{ systemMonitorApi.formatBytes(memoryBreakdown.app, 1) }}
            </div>
            <div
              class="stacked-bar-seg seg-last"
              :style="{ width: (memoryBreakdown.cache / Math.max(1, (store.overview?.memory.total || 1)) * 100) + '%', background: 'var(--success-color)' }"
            >
              {{ systemMonitorApi.formatBytes(memoryBreakdown.cache, 1) }}
            </div>
          </div>
          <div class="legend">
            <div class="legend-item">
              <span class="legend-dot" style="background: var(--info-color);" />
              <span class="legend-label">系统</span>
              <span class="legend-value">{{ systemMonitorApi.formatBytes(memoryBreakdown.system, 1) }}</span>
            </div>
            <div class="legend-item">
              <span class="legend-dot" style="background: var(--primary-color);" />
              <span class="legend-label">应用</span>
              <span class="legend-value">{{ systemMonitorApi.formatBytes(memoryBreakdown.app, 1) }}</span>
            </div>
            <div class="legend-item">
              <span class="legend-dot" style="background: var(--success-color);" />
              <span class="legend-label">缓存</span>
              <span class="legend-value">{{ systemMonitorApi.formatBytes(memoryBreakdown.cache, 1) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- ===== BOTTOM SECTION: Process Table ===== -->
      <div class="section-card">
        <div class="section-card-header">
          <svg
            width="14"
            height="14"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" /><line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" /><line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" />
          </svg>
          <span class="section-card-title">进程列表</span>
          <span class="section-card-badge">{{ filteredProcesses.length }} 个进程</span>
          <div class="process-search">
            <input
              v-model="processKeyword"
              class="search-input"
              type="text"
              placeholder="筛选进程..."
            />
          </div>
        </div>
        <div class="table-wrap">
          <table class="process-table">
            <thead>
              <tr>
                <th style="width: 32%;">
                  <button class="th-btn" @click="toggleSort('name')">
                    进程名 <span class="sort-icon">{{ sortIcon('name') }}</span>
                  </button>
                </th>
                <th style="width: 14%;">
                  <button class="th-btn" @click="toggleSort('pid')">
                    PID <span class="sort-icon">{{ sortIcon('pid') }}</span>
                  </button>
                </th>
                <th style="width: 14%;">
                  <button class="th-btn" @click="toggleSort('cpuUsage')">
                    CPU% <span class="sort-icon">{{ sortIcon('cpuUsage') }}</span>
                  </button>
                </th>
                <th style="width: 18%;">
                  <button class="th-btn" @click="toggleSort('memoryUsage')">
                    内存 <span class="sort-icon">{{ sortIcon('memoryUsage') }}</span>
                  </button>
                </th>
                <th style="width: 14%;">状态</th>
                <th style="width: 8%;" />
              </tr>
            </thead>
            <tbody>
              <tr v-for="proc in filteredProcesses" :key="proc.pid">
                <td class="cell-name">{{ proc.name }}</td>
                <td>{{ proc.pid }}</td>
                <td :class="{ 'cell-high': proc.cpuUsage > 5 }">{{ proc.cpuUsage.toFixed(1) }}</td>
                <td>{{ systemMonitorApi.formatBytes(proc.memoryBytes, 1) }}</td>
                <td>
                  <span class="status-badge">
                    <span class="status-dot-green" />
                    运行中
                  </span>
                </td>
                <td>
                  <button
                    class="kill-btn"
                    title="结束进程"
                    @click="handleKillProcess(proc.pid)"
                  >
                    ✕
                  </button>
                </td>
              </tr>
              <tr v-if="filteredProcesses.length === 0">
                <td colspan="6" class="empty-cell">暂无进程数据</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="bottom-spacer" />
    </div>
  </div>
</template>

<style scoped>
.system-monitor-view {
  position: relative;
  min-height: 100%;
  padding: 24px;
}

.ambient-glow {
  position: fixed;
  top: 60px;
  left: 40px;
  width: 600px;
  height: 500px;
  background: radial-gradient(ellipse at 30% 20%, rgba(245, 158, 11, 0.04) 0%, transparent 70%);
  pointer-events: none;
  z-index: 0;
}

.page-content {
  position: relative;
  z-index: 1;
  max-width: 1200px;
  margin: 0 auto;
}

/* ===== PAGE HEADER ===== */
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 12px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-icon {
  color: var(--primary-color);
  flex-shrink: 0;
}

.page-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.live-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 8px;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--success-color);
  background: rgba(52, 211, 153, 0.1);
  border: 1px solid rgba(52, 211, 153, 0.2);
  border-radius: 999px;
}

.live-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--success-color);
  animation: pulse-green 2s ease-in-out infinite;
}

@keyframes pulse-green {
  0%, 100% { box-shadow: 0 0 0 0 rgba(52, 211, 153, 0.5); }
  50% { box-shadow: 0 0 0 4px rgba(52, 211, 153, 0); }
}

.refresh-hint {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  background: var(--bg-tertiary);
  border-radius: 999px;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.connection-status.connected {
  background: rgba(52, 211, 153, 0.1);
  color: var(--success-color);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.update-time {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}

/* ===== ERROR BANNER ===== */
.error-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: rgba(248, 113, 113, 0.1);
  border: 1px solid rgba(248, 113, 113, 0.2);
  border-radius: var(--radius-md);
  color: var(--danger-color);
  margin-bottom: 16px;
  font-size: 0.875rem;
}

.error-msg { flex: 1; }

.error-close {
  background: none;
  border: none;
  color: var(--danger-color);
  cursor: pointer;
  opacity: 0.7;
  font-size: 14px;
  padding: 2px 6px;
}

.error-close:hover { opacity: 1; }

/* ===== STATS GRID ===== */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  transition: border-color var(--motion-fast), box-shadow var(--motion-fast);
}

.stat-card:hover {
  border-color: var(--primary-color);
  box-shadow: 0 0 16px rgba(245, 158, 11, 0.06);
}

.stat-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  color: var(--text-muted);
}

.stat-label {
  font-size: 0.75rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--text-secondary);
}

.stat-body {
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-body-col {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.stat-detail {
  display: flex;
  flex-direction: column;
}

.stat-value {
  font-family: var(--font-family-mono);
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--text-primary);
  line-height: 1;
}

.stat-sub {
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--text-muted);
  margin-top: 2px;
}

.stat-value-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.stat-big {
  font-family: var(--font-family-mono);
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--text-primary);
}

.stat-divider {
  font-family: var(--font-family-mono);
  font-size: 0.875rem;
  color: var(--text-muted);
}

.stat-dim {
  font-family: var(--font-family-mono);
  font-size: 0.875rem;
  color: var(--text-secondary);
}

/* ===== CPU GAUGE ===== */
.cpu-gauge {
  width: 72px;
  height: 72px;
  border-radius: 50%;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.cpu-gauge::after {
  content: '';
  width: 54px;
  height: 54px;
  border-radius: 50%;
  background: var(--bg-card);
  position: absolute;
}

.cpu-gauge-value {
  position: relative;
  z-index: 1;
  font-family: var(--font-family-mono);
  font-weight: 700;
  font-size: 1rem;
  color: var(--primary-color);
}

/* ===== HORIZONTAL BAR ===== */
.h-bar-track {
  width: 100%;
  height: 8px;
  border-radius: 999px;
  background: var(--bg-muted);
  overflow: hidden;
}

.h-bar-fill {
  height: 100%;
  border-radius: 999px;
  transition: width var(--motion-base);
}

.h-bar-labels {
  display: flex;
  justify-content: space-between;
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--text-muted);
}

/* ===== NETWORK ROWS ===== */
.net-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.net-up-icon { color: var(--primary-color); display: flex; }
.net-down-icon { color: var(--info-color); display: flex; }

.net-up-value {
  font-family: var(--font-family-mono);
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--primary-color);
}

.net-down-value {
  font-family: var(--font-family-mono);
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--info-color);
}

/* ===== MIDDLE GRID ===== */
.middle-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 24px;
}

.section-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
}

.section-card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  color: var(--primary-color);
}

.section-card-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--text-primary);
}

.section-card-badge {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
  margin-left: auto;
}

/* ===== BAR CHART ===== */
.bar-chart {
  display: flex;
  align-items: flex-end;
  gap: 6px;
  height: 120px;
  padding-top: 8px;
}

.bar {
  flex: 1;
  min-width: 0;
  border-radius: 3px 3px 0 0;
  background: var(--primary-color);
  opacity: 0.6;
  transition: opacity var(--motion-fast), height var(--motion-base);
  position: relative;
}

.bar:hover {
  opacity: 0.9;
}

.bar-current {
  opacity: 1;
  box-shadow: 0 0 10px rgba(245, 158, 11, 0.4);
}

.bar-axis {
  display: flex;
  justify-content: space-between;
  margin-top: 8px;
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--text-muted);
}

/* ===== STACKED BAR ===== */
.stacked-bar-track {
  width: 100%;
  height: 20px;
  border-radius: var(--radius-sm);
  background: var(--bg-muted);
  overflow: hidden;
  display: flex;
  margin-bottom: 16px;
}

.stacked-bar-seg {
  height: 100%;
  transition: width var(--motion-base);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  font-family: var(--font-family-mono);
  font-weight: 500;
  color: var(--text-primary);
  border-radius: var(--radius-sm) 0 0 var(--radius-sm);
}

.seg-last {
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
}

/* ===== LEGEND ===== */
.legend {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  flex-shrink: 0;
}

.legend-label {
  font-size: 0.875rem;
  color: var(--text-secondary);
}

.legend-value {
  margin-left: auto;
  font-family: var(--font-family-mono);
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--text-primary);
}

/* ===== PROCESS TABLE ===== */
.process-search {
  margin-left: auto;
}

.search-input {
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  padding: 4px 10px;
  font-size: 0.8125rem;
  color: var(--text-primary);
  width: 140px;
  outline: none;
  transition: border-color var(--motion-fast);
}

.search-input:focus {
  border-color: var(--primary-color);
}

.search-input::placeholder {
  color: var(--text-muted);
}

.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
}

.process-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.process-table thead th {
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 14px;
  text-align: left;
  border-bottom: 1px solid var(--border-color);
}

.th-btn {
  background: none;
  border: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 0;
}

.th-btn:hover {
  color: var(--text-primary);
}

.sort-icon {
  font-size: 0.7rem;
  opacity: 0.6;
}

.process-table tbody td {
  padding: 10px 14px;
  border-bottom: 1px solid var(--border-light);
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  color: var(--text-primary);
}

.process-table tbody td:first-child {
  font-family: var(--font-family-base);
}

.cell-name {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cell-high {
  color: var(--primary-color);
  font-weight: 600;
}

.process-table tbody tr:nth-child(even) {
  background: rgba(255, 255, 255, 0.015);
}

.process-table tbody tr:hover {
  background: var(--primary-soft);
}

/* ===== STATUS ===== */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.status-dot-green {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--success-color);
  display: inline-block;
  flex-shrink: 0;
}

.kill-btn {
  background: none;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 13px;
  padding: 2px 6px;
  border-radius: var(--radius-sm);
  transition: color var(--motion-fast), background var(--motion-fast);
  opacity: 0;
}

.process-table tbody tr:hover .kill-btn {
  opacity: 1;
}

.kill-btn:hover {
  color: var(--danger-color);
  background: rgba(248, 113, 113, 0.1);
}

.empty-cell {
  text-align: center;
  color: var(--text-muted);
  padding: 32px 14px !important;
}

.bottom-spacer {
  height: var(--space-8);
}

/* ===== RESPONSIVE ===== */
@media (max-width: 900px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  .middle-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .system-monitor-view {
    padding: 12px;
  }
  .stats-grid {
    grid-template-columns: 1fr;
  }
  .page-title {
    font-size: 1.375rem;
  }
}
</style>