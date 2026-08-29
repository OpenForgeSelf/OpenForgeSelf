<script setup lang="ts">
import { computed } from 'vue'
import type { MonitorOverview } from '@/types/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'

const props = defineProps<{
  overview: MonitorOverview | null
}>()

interface MetricCardData {
  title: string
  icon: string
  value: string
  unit: string
  percentage: number
  color: string
  bgColor: string
  trend: number
  subtitle: string
}

const cpuCard = computed<MetricCardData>(() => {
  const cpu = props.overview?.cpu
  return {
    title: 'CPU 使用率',
    icon: '⚡',
    value: cpu ? cpu.totalUsage.toFixed(1) : '0',
    unit: '%',
    percentage: cpu?.totalUsage || 0,
    color: '#1976d2',
    bgColor: '#e3f2fd',
    trend: 0,
    subtitle: cpu ? `${cpu.perCoreUsage.length} 核心` : '--'
  }
})

const memoryCard = computed<MetricCardData>(() => {
  const memory = props.overview?.memory
  return {
    title: '内存使用率',
    icon: '💾',
    value: memory ? memory.usagePercent.toFixed(1) : '0',
    unit: '%',
    percentage: memory?.usagePercent || 0,
    color: '#9c27b0',
    bgColor: '#f3e5f5',
    trend: 0,
    subtitle: memory
      ? `${systemMonitorApi.formatBytes(memory.used)} / ${systemMonitorApi.formatBytes(memory.total)}`
      : '--'
  }
})

const diskCard = computed<MetricCardData>(() => {
  const disks = props.overview?.disks || []
  const totalUsed = disks.reduce((sum, d) => sum + d.usedSpace, 0)
  const totalSize = disks.reduce((sum, d) => sum + d.totalSize, 0)
  const percentage = totalSize > 0 ? (totalUsed / totalSize) * 100 : 0
  return {
    title: '磁盘使用率',
    icon: '💿',
    value: percentage.toFixed(1),
    unit: '%',
    percentage,
    color: '#ff9800',
    bgColor: '#fff3e0',
    trend: 0,
    subtitle: `${disks.length} 个分区`
  }
})

const networkCard = computed<MetricCardData>(() => {
  const network = props.overview?.network
  return {
    title: '网络速度',
    icon: '🌐',
    value: network ? systemMonitorApi.formatSpeed(network.downloadSpeed) : '0 B/s',
    unit: '',
    percentage: 0,
    color: '#4caf50',
    bgColor: '#e8f5e9',
    trend: 0,
    subtitle: network ? `上行: ${systemMonitorApi.formatSpeed(network.uploadSpeed)}` : '--'
  }
})

const cards = computed(() => [cpuCard.value, memoryCard.value, diskCard.value, networkCard.value])

function getStrokeDashoffset(percentage: number, circumference: number): number {
  return circumference - (percentage / 100) * circumference
}

const CIRCUMFERENCE = 2 * Math.PI * 40
</script>

<template>
  <div class="overview-cards" role="region" aria-label="系统概览">
    <div
      v-for="(card, index) in cards"
      :key="index"
      class="metric-card"
      :style="{ '--card-color': card.color, '--card-bg': card.bgColor }"
    >
      <div class="card-header">
        <span class="card-icon">{{ card.icon }}</span>
        <span class="card-title">{{ card.title }}</span>
      </div>
      <div class="card-body">
        <div class="progress-ring-container">
          <svg class="progress-ring" viewBox="0 0 100 100" aria-hidden="true">
            <circle
              class="progress-ring-bg"
              cx="50"
              cy="50"
              r="40"
              fill="none"
              stroke-width="8"
            />
            <circle
              class="progress-ring-fill"
              cx="50"
              cy="50"
              r="40"
              fill="none"
              stroke-width="8"
              stroke-linecap="round"
              :stroke-dasharray="CIRCUMFERENCE"
              :stroke-dashoffset="getStrokeDashoffset(card.percentage, CIRCUMFERENCE)"
              transform="rotate(-90 50 50)"
            />
          </svg>
          <div class="progress-text">
            <span class="progress-value">{{ card.value }}</span>
            <span class="progress-unit">{{ card.unit }}</span>
          </div>
        </div>
        <div class="card-info">
          <div class="card-subtitle">{{ card.subtitle }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.overview-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.metric-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.metric-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
}

.card-header {
  display: flex;
  align-items: center;
  gap: 10px;
}

.card-icon {
  font-size: 24px;
}

.card-title {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
}

.card-body {
  display: flex;
  align-items: center;
  gap: 16px;
}

.progress-ring-container {
  position: relative;
  width: 90px;
  height: 90px;
  flex-shrink: 0;
}

.progress-ring {
  width: 100%;
  height: 100%;
}

.progress-ring-bg {
  stroke: #f1f3f5;
}

.progress-ring-fill {
  stroke: var(--card-color);
  transition: stroke-dashoffset 0.5s ease;
}

.progress-text {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  text-align: center;
}

.progress-value {
  font-size: 18px;
  font-weight: 700;
  color: var(--card-color);
}

.progress-unit {
  font-size: 12px;
  color: #6c757d;
}

.card-info {
  flex: 1;
  min-width: 0;
}

.card-subtitle {
  font-size: 13px;
  color: #6c757d;
  line-height: 1.5;
  word-break: break-all;
}

@media (max-width: 768px) {
  .overview-cards {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 480px) {
  .overview-cards {
    grid-template-columns: 1fr;
  }
}
</style>
