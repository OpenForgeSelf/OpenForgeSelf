<script setup lang="ts">
import type { WorkflowStatus } from '@/types/workflow'

interface StepInfo {
  stepId: string
  stepName: string
  status: WorkflowStatus
  startTime?: Date
  endTime?: Date
  result?: unknown
  error?: string
}

defineProps<{
  steps: StepInfo[]
  currentStepId?: string
}>()

const emit = defineEmits<{
  (e: 'stepClick', step: StepInfo): void
}>()

function getStatusLabel(status: WorkflowStatus): string {
  const labels: Record<WorkflowStatus, string> = {
    draft: '待执行',
    ready: '就绪',
    running: '执行中',
    paused: '已暂停',
    completed: '已完成',
    failed: '失败',
    cancelled: '已取消'
  }
  return labels[status] || status
}

function getStatusIcon(status: WorkflowStatus): string {
  const icons: Record<WorkflowStatus, string> = {
    draft: '○',
    ready: '○',
    running: '◐',
    paused: '⏸',
    completed: '✓',
    failed: '✕',
    cancelled: '⊘'
  }
  return icons[status] || '○'
}

function formatDuration(start?: Date, end?: Date): string {
  if (!start) return '-'
  const endTime = end || new Date()
  const durationMs = endTime.getTime() - new Date(start).getTime()
  
  if (durationMs < 1000) {
    return `${durationMs}ms`
  } else if (durationMs < 60000) {
    return `${(durationMs / 1000).toFixed(1)}s`
  } else {
    const minutes = Math.floor(durationMs / 60000)
    const seconds = ((durationMs % 60000) / 1000).toFixed(0)
    return `${minutes}m${seconds}s`
  }
}

function handleStepClick(step: StepInfo): void {
  emit('stepClick', step)
}
</script>

<template>
  <div class="step-timeline">
    <div class="timeline-header">
      <h4>执行进度</h4>
      <span class="step-count">{{ steps.filter(s => s.status === 'completed').length }} / {{ steps.length }}</span>
    </div>
    <div class="timeline-list">
      <div
        v-for="(step, index) in steps"
        :key="step.stepId"
        class="timeline-item"
        :class="{
          active: currentStepId === step.stepId || step.status === 'running',
          completed: step.status === 'completed',
          failed: step.status === 'failed',
          cancelled: step.status === 'cancelled'
        }"
        @click="handleStepClick(step)"
      >
        <div class="timeline-line">
          <div class="line-top" :class="{ filled: step.status === 'completed' }" />
          <div class="timeline-node" :class="step.status">
            <span class="node-icon">{{ getStatusIcon(step.status) }}</span>
          </div>
          <div class="line-bottom" :class="{ filled: step.status === 'completed' && index < steps.length - 1 }" />
        </div>
        <div class="timeline-content">
          <div class="step-header">
            <span class="step-name">{{ step.stepName }}</span>
            <span class="step-status" :class="step.status">
              {{ getStatusLabel(step.status) }}
            </span>
          </div>
          <div v-if="step.startTime" class="step-meta">
            <span class="step-duration">
              耗时: {{ formatDuration(step.startTime, step.endTime) }}
            </span>
          </div>
          <div v-if="step.error" class="step-error">
            {{ step.error }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.step-timeline {
  background: #fff;
  border-radius: 8px;
  padding: 16px;
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.timeline-header h4 {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.step-count {
  font-size: 13px;
  color: #6c757d;
  background: #f1f3f5;
  padding: 2px 10px;
  border-radius: 12px;
}

.timeline-list {
  display: flex;
  flex-direction: column;
}

.timeline-item {
  display: flex;
  gap: 12px;
  cursor: pointer;
  transition: background-color 0.2s ease;
  border-radius: 6px;
}

.timeline-item:hover {
  background: #f8f9fa;
}

.timeline-line {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 24px;
  flex-shrink: 0;
}

.line-top,
.line-bottom {
  flex: 1;
  width: 2px;
  background: #dee2e6;
  min-height: 8px;
}

.line-top.filled,
.line-bottom.filled {
  background: #28a745;
}

.timeline-node {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: #fff;
  border: 2px solid #dee2e6;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.2s ease;
}

.node-icon {
  font-size: 10px;
  font-weight: 600;
  color: #adb5bd;
}

.timeline-node.completed {
  border-color: #28a745;
  background: #28a745;
}

.timeline-node.completed .node-icon {
  color: #fff;
}

.timeline-node.running {
  border-color: #007bff;
  background: #fff;
  animation: pulse-node 1.5s ease-in-out infinite;
}

@keyframes pulse-node {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(0, 123, 255, 0.4);
  }
  50% {
    box-shadow: 0 0 0 6px rgba(0, 123, 255, 0);
  }
}

.timeline-node.running .node-icon {
  color: #007bff;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.timeline-node.failed {
  border-color: #dc3545;
  background: #dc3545;
}

.timeline-node.failed .node-icon {
  color: #fff;
}

.timeline-node.paused {
  border-color: #ffc107;
  background: #fff;
}

.timeline-node.paused .node-icon {
  color: #ffc107;
}

.timeline-node.cancelled {
  border-color: #6c757d;
  background: #6c757d;
}

.timeline-node.cancelled .node-icon {
  color: #fff;
}

.timeline-content {
  flex: 1;
  padding: 2px 0 16px 0;
  min-width: 0;
}

.timeline-item:last-child .timeline-content {
  padding-bottom: 0;
}

.step-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.step-name {
  font-size: 14px;
  font-weight: 500;
  color: #212529;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.timeline-item.active .step-name {
  color: #007bff;
  font-weight: 600;
}

.step-status {
  font-size: 11px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 10px;
  flex-shrink: 0;
}

.step-status.completed {
  background: #d4edda;
  color: #155724;
}

.step-status.running {
  background: #cce5ff;
  color: #004085;
}

.step-status.failed {
  background: #f8d7da;
  color: #721c24;
}

.step-status.paused {
  background: #fff3cd;
  color: #856404;
}

.step-status.cancelled {
  background: #e2e3e5;
  color: #383d41;
}

.step-status.draft,
.step-status.ready {
  background: #f1f3f5;
  color: #6c757d;
}

.step-meta {
  font-size: 12px;
  color: #6c757d;
  margin-top: 4px;
}

.step-duration {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.step-error {
  font-size: 12px;
  color: #dc3545;
  margin-top: 4px;
  padding: 6px 8px;
  background: #fff5f5;
  border-radius: 4px;
  border-left: 3px solid #dc3545;
}
</style>
