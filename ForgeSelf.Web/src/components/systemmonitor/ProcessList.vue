<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ProcessInfo, ProcessSortField, SortDirection } from '@/types/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'

const props = defineProps<{
  processes: ProcessInfo[]
  loading?: boolean
}>()

const emit = defineEmits<{
  kill: [pid: number]
  refresh: []
}>()

const searchKeyword = ref('')
const sortField = ref<ProcessSortField>('cpuUsage')
const sortDirection = ref<SortDirection>('desc')
const confirmPid = ref<number | null>(null)

const sortedProcesses = computed(() => {
  let result = [...props.processes]

  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(p =>
      p.name.toLowerCase().includes(keyword) ||
      String(p.pid).includes(keyword)
    )
  }

  result.sort((a, b) => {
    let comparison = 0
    switch (sortField.value) {
      case 'name':
        comparison = a.name.localeCompare(b.name)
        break
      case 'cpuUsage':
        comparison = a.cpuUsage - b.cpuUsage
        break
      case 'memoryUsage':
        comparison = a.memoryBytes - b.memoryBytes
        break
      case 'pid':
        comparison = a.pid - b.pid
        break
    }
    return sortDirection.value === 'asc' ? comparison : -comparison
  })

  return result
})

function toggleSort(field: ProcessSortField): void {
  if (sortField.value === field) {
    sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortField.value = field
    sortDirection.value = 'desc'
  }
}

function getSortIndicator(field: ProcessSortField): string {
  if (sortField.value !== field) return '↕'
  return sortDirection.value === 'asc' ? '↑' : '↓'
}

function confirmKill(pid: number): void {
  confirmPid.value = pid
}

function cancelKill(): void {
  confirmPid.value = null
}

function executeKill(pid: number): void {
  emit('kill', pid)
  confirmPid.value = null
}
</script>

<template>
  <div class="process-list">
    <div class="list-header">
      <h3 class="list-title">进程列表</h3>
      <div class="list-actions">
        <div class="search-box">
          <input
            v-model="searchKeyword"
            type="text"
            class="search-input"
            placeholder="搜索进程..."
            aria-label="搜索进程"
          />
        </div>
        <button
          class="refresh-btn"
          :disabled="loading"
          aria-label="刷新进程列表"
          @click="emit('refresh')"
        >
          {{ loading ? '刷新中...' : '刷新' }}
        </button>
      </div>
    </div>

    <div class="table-container">
      <table class="process-table" role="table">
        <thead>
          <tr>
            <th
              class="sortable"
              role="columnheader"
              :aria-sort="sortField === 'name' ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="toggleSort('name')"
            >
              进程名 <span class="sort-indicator">{{ getSortIndicator('name') }}</span>
            </th>
            <th
              class="sortable"
              role="columnheader"
              :aria-sort="sortField === 'pid' ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="toggleSort('pid')"
            >
              PID <span class="sort-indicator">{{ getSortIndicator('pid') }}</span>
            </th>
            <th
              class="sortable"
              role="columnheader"
              :aria-sort="sortField === 'cpuUsage' ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="toggleSort('cpuUsage')"
            >
              CPU <span class="sort-indicator">{{ getSortIndicator('cpuUsage') }}</span>
            </th>
            <th
              class="sortable"
              role="columnheader"
              :aria-sort="sortField === 'memoryUsage' ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="toggleSort('memoryUsage')"
            >
              内存 <span class="sort-indicator">{{ getSortIndicator('memoryUsage') }}</span>
            </th>
            <th>运行时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="6" class="loading-cell">
              <div class="loading-text">加载中...</div>
            </td>
          </tr>
          <tr v-else-if="sortedProcesses.length === 0">
            <td colspan="6" class="empty-cell">
              <div class="empty-text">没有找到进程</div>
            </td>
          </tr>
          <tr v-for="process in sortedProcesses" v-else :key="process.pid" class="process-row">
            <td class="process-name">{{ process.name }}</td>
            <td class="process-pid">{{ process.pid }}</td>
            <td>
              <div class="usage-cell">
                <div class="usage-bar">
                  <div
                    class="usage-fill cpu"
                    :style="{ width: Math.min(process.cpuUsage, 100) + '%' }"
                  />
                </div>
                <span class="usage-value">{{ process.cpuUsage.toFixed(1) }}%</span>
              </div>
            </td>
            <td>
              <div class="usage-cell">
                <div class="usage-bar">
                  <div
                    class="usage-fill memory"
                    :style="{ width: Math.min(process.memoryUsage, 100) + '%' }"
                  />
                </div>
                <span class="usage-value">{{ systemMonitorApi.formatBytes(process.memoryBytes) }}</span>
              </div>
            </td>
            <td class="uptime">{{ systemMonitorApi.formatUptime(process.startTime) }}</td>
            <td class="action-cell">
              <button
                v-if="confirmPid !== process.pid"
                class="kill-btn"
                aria-label="结束进程"
                @click="confirmKill(process.pid)"
              >
                结束
              </button>
              <div v-else class="confirm-actions">
                <button
                  class="confirm-btn yes"
                  aria-label="确认结束进程"
                  @click="executeKill(process.pid)"
                >
                  确认
                </button>
                <button
                  class="confirm-btn no"
                  aria-label="取消结束进程"
                  @click="cancelKill"
                >
                  取消
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.process-list {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 400px;
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-shrink: 0;
}

.list-title {
  font-size: 16px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.list-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.search-box {
  position: relative;
}

.search-input {
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 14px;
  width: 200px;
  transition: border-color 0.2s;
}

.search-input:focus {
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

.refresh-btn:hover:not(:disabled) {
  background: #1565c0;
}

.refresh-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.table-container {
  flex: 1;
  overflow: auto;
  min-height: 0;
}

.process-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.process-table th,
.process-table td {
  padding: 12px 8px;
  text-align: left;
  border-bottom: 1px solid #f1f3f5;
}

.process-table th {
  background: #f8f9fa;
  font-weight: 600;
  color: #495057;
  position: sticky;
  top: 0;
  z-index: 1;
}

.sortable {
  cursor: pointer;
  user-select: none;
}

.sortable:hover {
  color: #1976d2;
}

.sort-indicator {
  font-size: 12px;
  margin-left: 4px;
  opacity: 0.6;
}

.process-row:hover {
  background: #f8f9fa;
}

.process-name {
  font-weight: 500;
  color: #212529;
}

.process-pid {
  color: #6c757d;
  font-family: monospace;
}

.usage-cell {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 150px;
}

.usage-bar {
  flex: 1;
  height: 8px;
  background: #e9ecef;
  border-radius: 4px;
  overflow: hidden;
}

.usage-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.3s ease;
}

.usage-fill.cpu {
  background: linear-gradient(90deg, #4caf50, #ff9800, #f44336);
}

.usage-fill.memory {
  background: linear-gradient(90deg, #2196f3, #9c27b0);
}

.usage-value {
  font-size: 13px;
  color: #495057;
  min-width: 70px;
  text-align: right;
}

.uptime {
  color: #6c757d;
  font-size: 13px;
}

.action-cell {
  width: 120px;
}

.kill-btn {
  padding: 6px 12px;
  background: #f44336;
  color: #fff;
  border: none;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.kill-btn:hover {
  background: #d32f2f;
}

.confirm-actions {
  display: flex;
  gap: 6px;
}

.confirm-btn {
  padding: 6px 10px;
  border: none;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.confirm-btn.yes {
  background: #f44336;
  color: #fff;
}

.confirm-btn.yes:hover {
  background: #d32f2f;
}

.confirm-btn.no {
  background: #e9ecef;
  color: #495057;
}

.confirm-btn.no:hover {
  background: #dee2e6;
}

.loading-cell,
.empty-cell {
  text-align: center;
  padding: 40px 20px;
}

.loading-text,
.empty-text {
  color: #6c757d;
  font-size: 14px;
}

@media (max-width: 768px) {
  .list-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .list-actions {
    width: 100%;
  }

  .search-input {
    flex: 1;
    width: 100%;
  }

  .process-table {
    font-size: 12px;
  }

  .process-table th,
  .process-table td {
    padding: 8px 4px;
  }
}
</style>
