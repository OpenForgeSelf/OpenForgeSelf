<script setup lang="ts">
import type { DiskDrive } from '@/types/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'

defineProps<{
  disks: DiskDrive[]
}>()

function getUsageClass(percent: number): string {
  if (percent >= 90) return 'danger'
  if (percent >= 70) return 'warning'
  return 'normal'
}

function isWarning(percent: number): boolean {
  return percent >= 90
}
</script>

<template>
  <div class="disk-detail">
    <h3 class="detail-title">磁盘详情</h3>
    <div v-if="disks.length === 0" class="empty-state">
      <p>暂无磁盘数据</p>
    </div>
    <div v-else class="disk-list">
      <div
        v-for="disk in disks"
        :key="disk.name"
        class="disk-item"
        :class="{ warning: isWarning(disk.usagePercent) }"
      >
        <div class="disk-header">
          <div class="disk-info">
            <span class="disk-name">{{ disk.name }}</span>
            <span class="disk-label">{{ disk.label }}</span>
            <span v-if="isWarning(disk.usagePercent)" class="warning-badge">
              ⚠️ 空间不足
            </span>
          </div>
          <div class="disk-usage-percent" :class="getUsageClass(disk.usagePercent)">
            {{ disk.usagePercent.toFixed(1) }}%
          </div>
        </div>

        <div class="progress-bar-container">
          <div class="progress-bar-bg">
            <div
              class="progress-bar-fill"
              :class="getUsageClass(disk.usagePercent)"
              :style="{ width: disk.usagePercent + '%' }"
            />
          </div>
        </div>

        <div class="disk-stats">
          <div class="stat-item">
            <span class="stat-label">总容量</span>
            <span class="stat-value">{{ systemMonitorApi.formatBytes(disk.totalSize) }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">已用</span>
            <span class="stat-value used">{{ systemMonitorApi.formatBytes(disk.usedSpace) }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">可用</span>
            <span class="stat-value free">{{ systemMonitorApi.formatBytes(disk.freeSpace) }}</span>
          </div>
        </div>

        <div class="disk-io">
          <div class="io-item read">
            <span class="io-icon">📥</span>
            <span class="io-label">读取</span>
            <span class="io-value">{{ systemMonitorApi.formatSpeed(disk.readSpeed) }}</span>
          </div>
          <div class="io-item write">
            <span class="io-icon">📤</span>
            <span class="io-label">写入</span>
            <span class="io-value">{{ systemMonitorApi.formatSpeed(disk.writeSpeed) }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.disk-detail {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.detail-title {
  font-size: 16px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 20px 0;
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
  color: #6c757d;
}

.disk-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.disk-item {
  border: 1px solid #e9ecef;
  border-radius: 10px;
  padding: 16px;
  transition: all 0.2s ease;
}

.disk-item:hover {
  border-color: #dee2e6;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.disk-item.warning {
  border-color: #ffcdd2;
  background: #fffafa;
}

.disk-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.disk-info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.disk-name {
  font-size: 16px;
  font-weight: 600;
  color: #212529;
}

.disk-label {
  font-size: 14px;
  color: #6c757d;
}

.warning-badge {
  font-size: 12px;
  color: #d32f2f;
  background: #ffebee;
  padding: 2px 8px;
  border-radius: 4px;
  font-weight: 500;
}

.disk-usage-percent {
  font-size: 20px;
  font-weight: 700;
}

.disk-usage-percent.normal {
  color: #4caf50;
}

.disk-usage-percent.warning {
  color: #ff9800;
}

.disk-usage-percent.danger {
  color: #f44336;
}

.progress-bar-container {
  margin-bottom: 12px;
}

.progress-bar-bg {
  height: 10px;
  background: #e9ecef;
  border-radius: 5px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  border-radius: 5px;
  transition: width 0.5s ease;
}

.progress-bar-fill.normal {
  background: linear-gradient(90deg, #4caf50, #8bc34a);
}

.progress-bar-fill.warning {
  background: linear-gradient(90deg, #ff9800, #ffc107);
}

.progress-bar-fill.danger {
  background: linear-gradient(90deg, #f44336, #e91e63);
}

.disk-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-bottom: 12px;
}

.stat-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.stat-label {
  font-size: 12px;
  color: #6c757d;
}

.stat-value {
  font-size: 14px;
  font-weight: 500;
  color: #212529;
}

.stat-value.used {
  color: #ff9800;
}

.stat-value.free {
  color: #4caf50;
}

.disk-io {
  display: flex;
  gap: 16px;
  padding-top: 12px;
  border-top: 1px solid #f1f3f5;
}

.io-item {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
}

.io-icon {
  font-size: 16px;
}

.io-label {
  font-size: 13px;
  color: #6c757d;
}

.io-value {
  font-size: 13px;
  font-weight: 500;
  color: #212529;
  margin-left: auto;
}

.io-item.read .io-value {
  color: #2196f3;
}

.io-item.write .io-value {
  color: #9c27b0;
}

@media (max-width: 640px) {
  .disk-stats {
    grid-template-columns: 1fr;
    gap: 8px;
  }

  .disk-io {
    flex-direction: column;
    gap: 8px;
  }
}
</style>
