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
    color: '#2196f3',
    fill: true
  },
  {
    label: '上传',
    data: props.history.map(h => ({ value: h.upload, timestamp: h.timestamp })),
    color: '#4caf50',
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
  color: #212529;
  margin: 0;
}

.speed-section {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
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
  background: #f8f9fa;
}

.speed-card.download {
  background: linear-gradient(135deg, #e3f2fd 0%, #bbdefb 100%);
}

.speed-card.upload {
  background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
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
  color: #6c757d;
}

.speed-value {
  font-size: 24px;
  font-weight: 700;
  color: #212529;
}

.chart-section {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.connections-section {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
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
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 14px;
  width: 180px;
  transition: border-color 0.2s;
}

.filter-input:focus {
  border-color: #1976d2;
  outline: none;
}

.refresh-btn {
  padding: 8px 16px;
  background: #1976d2;
  color: #fff;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.refresh-btn:hover {
  background: #1565c0;
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
  border-bottom: 1px solid #f1f3f5;
}

.connections-table th {
  background: #f8f9fa;
  font-weight: 600;
  color: #495057;
  position: sticky;
  top: 0;
  z-index: 1;
}

.connection-row:hover {
  background: #f8f9fa;
}

.protocol {
  font-weight: 500;
  color: #1976d2;
  font-family: monospace;
}

.address {
  font-family: monospace;
  color: #495057;
}

.state-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.process {
  color: #6c757d;
}

.empty-cell {
  text-align: center;
  padding: 30px 20px;
  color: #6c757d;
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
