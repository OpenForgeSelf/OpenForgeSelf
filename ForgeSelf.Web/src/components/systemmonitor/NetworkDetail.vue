<script setup lang="ts">
import { ref, computed } from 'vue'
import type { NetworkSpeed, NetworkConnection } from '@/types/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'
import TrendChart from './TrendChart.vue'
import type { ChartDataset } from './TrendChart.vue'

const props = defineProps<{
  networkSpeed: NetworkSpeed | null
  connections: NetworkConnection[]
  history: { download: number; upload: number; timestamp: number }[]
}>()

const emit = defineEmits<{
  refreshConnections: []
}>()

const connectionFilter = ref('')

const filteredConnections = computed(() => {
  if (!connectionFilter.value) return props.connections
  const keyword = connectionFilter.value.toLowerCase()
  return props.connections.filter(c =>
    c.protocol.toLowerCase().includes(keyword) ||
    c.localAddress.toLowerCase().includes(keyword) ||
    c.remoteAddress.toLowerCase().includes(keyword) ||
    c.state.toLowerCase().includes(keyword) ||
    c.processName.toLowerCase().includes(keyword)
  )
})

const chartDatasets = computed<ChartDataset[]>(() => [
  {
    label: '下载',
    data: props.history.map(h => ({ value: h.download, timestamp: h.timestamp })),
    color: 'var(--el-color-primary)',
    fill: true
  },
  {
    label: '上传',
    data: props.history.map(h => ({ value: h.upload, timestamp: h.timestamp })),
    color: 'var(--el-color-success)',
    fill: true
  }
])

function formatSpeedValue(value: number): string {
  return systemMonitorApi.formatSpeed(value)
}

function getStateColor(state: string): string {
  switch (state.toUpperCase()) {
    case 'ESTABLISHED':
      return '#4caf50'
    case 'LISTENING':
      return '#2196f3'
    case 'TIME_WAIT':
    case 'CLOSE_WAIT':
      return '#ff9800'
    case 'CLOSED':
      return '#9e9e9e'
    default:
      return '#6c757d'
  }
}
</script>

<template>
  <div class="network-detail">
    <div class="speed-section">
      <h3 class="section-title">网络速度</h3>
      <div class="speed-cards">
        <div class="speed-card download">
          <div class="speed-icon">⬇️</div>
          <div class="speed-info">
            <div class="speed-label">下载速度</div>
            <div class="speed-value">
              {{ networkSpeed ? systemMonitorApi.formatSpeed(networkSpeed.downloadSpeed) : '--' }}
            </div>
          </div>
        </div>
        <div class="speed-card upload">
          <div class="speed-icon">⬆️</div>
          <div class="speed-info">
            <div class="speed-label">上传速度</div>
            <div class="speed-value">
              {{ networkSpeed ? systemMonitorApi.formatSpeed(networkSpeed.uploadSpeed) : '--' }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="chart-section">
      <TrendChart
        :datasets="chartDatasets"
        title="流量趋势"
        :height="200"
        :show-legend="true"
        :format-value="formatSpeedValue"
      />
    </div>

    <div class="connections-section">
      <div class="section-header">
        <h3 class="section-title">网络连接 ({{ connections.length }})</h3>
        <div class="section-actions">
          <input
            v-model="connectionFilter"
            type="text"
            class="filter-input"
            placeholder="搜索连接..."
            aria-label="搜索网络连接"
          />
          <button class="refresh-btn" aria-label="刷新连接列表" @click="emit('refreshConnections')">
            刷新
          </button>
        </div>
      </div>

      <div class="connections-table-container">
        <table class="connections-table" role="table">
          <thead>
            <tr>
              <th>协议</th>
              <th>本地地址</th>
              <th>远程地址</th>
              <th>状态</th>
              <th>进程</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredConnections.length === 0">
              <td colspan="5" class="empty-cell">
                <span>暂无连接数据</span>
              </td>
            </tr>
            <tr v-for="(conn, index) in filteredConnections" v-else :key="index" class="connection-row">
              <td class="protocol">{{ conn.protocol }}</td>
              <td class="address">{{ conn.localAddress }}</td>
              <td class="address">{{ conn.remoteAddress }}</td>
              <td>
                <span
                  class="state-badge"
                  :style="{ backgroundColor: getStateColor(conn.state) + '20', color: getStateColor(conn.state) }"
                >
                  {{ conn.state }}
                </span>
              </td>
              <td class="process">{{ conn.processName }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.network-detail {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.speed-section {
  background: var(--el-bg-color);
  border-radius: 12px;
  padding: 20px;
  box-shadow: var(--el-box-shadow-light);
}

.speed-cards {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  margin-top: 16px;
}

.speed-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  border-radius: 10px;
  background: var(--el-fill-color-light);
}

.speed-card.download {
  background: linear-gradient(135deg, var(--el-color-info-light-9), var(--el-color-info-light-8));
}

.speed-card.upload {
  background: linear-gradient(135deg, var(--el-color-success-light-9), var(--el-color-success-light-8));
}

.speed-icon {
  font-size: 32px;
}

.speed-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.speed-label {
  font-size: 14px;
  color: var(--el-text-color-secondary);
}

.speed-value {
  font-size: 24px;
  font-weight: 700;
  color: var(--el-text-color-primary);
}

.chart-section {
  background: var(--el-bg-color);
  border-radius: 12px;
  padding: 20px;
  box-shadow: var(--el-box-shadow-light);
}

.connections-section {
  background: var(--el-bg-color);
  border-radius: 12px;
  padding: 20px;
  box-shadow: var(--el-box-shadow-light);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 12px;
}

.section-actions {
  display: flex;
  gap: 10px;
  align-items: center;
}

.filter-input {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 14px;
  width: 180px;
  transition: border-color 0.2s;
}

.filter-input:focus {
  border-color: var(--el-color-primary);
  outline: none;
}

.refresh-btn {
  padding: 8px 16px;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  border: none;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.refresh-btn:hover {
  background: var(--el-color-primary-dark-2);
}

.connections-table-container {
  max-height: 300px;
  overflow: auto;
}

.connections-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.connections-table th,
.connections-table td {
  padding: 10px 8px;
  text-align: left;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.connections-table th {
  background: var(--el-fill-color-light);
  font-weight: 600;
  color: var(--el-text-color-regular);
  position: sticky;
  top: 0;
  z-index: 1;
}

.connection-row:hover {
  background: var(--el-fill-color-light);
}

.protocol {
  font-weight: 500;
  color: var(--el-color-primary);
  font-family: monospace;
}

.address {
  font-family: monospace;
  color: var(--el-text-color-regular);
}

.state-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.process {
  color: var(--el-text-color-secondary);
}

.empty-cell {
  text-align: center;
  padding: 30px 20px;
  color: var(--el-text-color-secondary);
}

@media (max-width: 768px) {
  .speed-cards {
    grid-template-columns: 1fr;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .filter-input {
    width: 100%;
  }

  .connections-table {
    font-size: 12px;
  }

  .connections-table th,
  .connections-table td {
    padding: 8px 4px;
  }
}
</style>
