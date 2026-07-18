<script setup lang="ts">
import { computed } from 'vue'
import type { WorkflowDefinition } from '@/types/workflow'

const props = defineProps<{
  workflow: WorkflowDefinition
}>()

const emit = defineEmits<{
  (e: 'click', workflow: WorkflowDefinition): void
  (e: 'execute', workflow: WorkflowDefinition): void
  (e: 'edit', workflow: WorkflowDefinition): void
  (e: 'delete', workflow: WorkflowDefinition): void
  (e: 'toggleFavorite', workflow: WorkflowDefinition): void
}>()


const statusLabel = computed(() => {
  switch (props.workflow.status) {
    case 'draft':
      return '草稿'
    case 'ready':
      return '就绪'
    case 'running':
      return '运行中'
    case 'paused':
      return '已暂停'
    case 'completed':
      return '已完成'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return '未知'
  }
})

const statusClass = computed(() => {
  return `status-${props.workflow.status}`
})

function handleCardClick(): void {
  emit('click', props.workflow)
}

function handleExecuteClick(event: Event): void {
  event.stopPropagation()
  emit('execute', props.workflow)
}

function handleEditClick(event: Event): void {
  event.stopPropagation()
  emit('edit', props.workflow)
}

function handleDeleteClick(event: Event): void {
  event.stopPropagation()
  emit('delete', props.workflow)
}

function handleFavoriteClick(event: Event): void {
  event.stopPropagation()
  emit('toggleFavorite', props.workflow)
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    handleCardClick()
  }
}
</script>

<template>
  <div
    class="workflow-card"
    role="button"
    tabindex="0"
    :aria-label="`工作流 ${workflow.name}，状态 ${statusLabel}`"
    @click="handleCardClick"
    @keydown="handleKeydown"
  >
    <div class="card-header">
      <div class="workflow-icon">
        <span class="icon-fallback">{{ workflow.icon || '⚙️' }}</span>
      </div>
      <div class="workflow-basic">
        <h3 class="workflow-name">{{ workflow.name }}</h3>
        <span class="workflow-steps">{{ workflow.steps.length }} 个步骤</span>
      </div>
      <button
        class="favorite-btn"
        :class="{ active: workflow.isFavorite }"
        :aria-label="workflow.isFavorite ? '取消收藏' : '收藏'"
        @click="handleFavoriteClick"
      >
        {{ workflow.isFavorite ? '⭐' : '☆' }}
      </button>
    </div>

    <div class="card-body">
      <p class="workflow-description">{{ workflow.description || '暂无描述' }}</p>
    </div>

    <div class="card-footer">
      <div class="workflow-meta">
        <span v-if="workflow.category" class="workflow-category">{{ workflow.category }}</span>
        <span class="workflow-usage">使用 {{ workflow.usageCount }} 次</span>
        <span class="workflow-status" :class="statusClass">
          <span class="status-dot" />
          {{ statusLabel }}
        </span>
      </div>
      <div class="card-actions">
        <button
          class="action-btn execute-btn"
          :disabled="workflow.status === 'running'"
          aria-label="执行工作流"
          @click="handleExecuteClick"
        >
          ▶
        </button>
        <div class="more-actions">
          <button
            class="action-btn"
            aria-label="编辑工作流"
            @click="handleEditClick"
          >
            ✏️
          </button>
          <button
            class="action-btn delete-btn"
            aria-label="删除工作流"
            @click="handleDeleteClick"
          >
            🗑️
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.workflow-card {
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 12px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.25s ease;
  display: flex;
  flex-direction: column;
  gap: 12px;
  outline: none;
}

.workflow-card:hover {
  border-color: #1976d2;
  box-shadow: 0 4px 12px rgba(25, 118, 210, 0.12);
  transform: translateY(-2px);
}

.workflow-card:focus-visible {
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.2);
}

.card-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.workflow-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background: #f0f7ff;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.icon-fallback {
  font-size: 24px;
}

.workflow-basic {
  flex: 1;
  min-width: 0;
}

.workflow-name {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 4px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.workflow-steps {
  font-size: 12px;
  color: #6c757d;
}

.favorite-btn {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  padding: 4px;
  flex-shrink: 0;
  transition: transform 0.2s ease;
}

.favorite-btn:hover {
  transform: scale(1.1);
}

.favorite-btn.active {
  color: #ffc107;
}

.card-body {
  flex: 1;
}

.workflow-description {
  font-size: 13px;
  color: #495057;
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-top: 12px;
  border-top: 1px solid #f1f3f5;
}

.workflow-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 12px;
  color: #6c757d;
  align-items: center;
}

.workflow-category {
  background: #f1f3f5;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 11px;
}

.workflow-usage {
  color: #6c757d;
}

.workflow-status {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #6c757d;
}

.status-draft .status-dot {
  background: #6c757d;
}

.status-ready .status-dot {
  background: #17a2b8;
}

.status-running .status-dot {
  background: #007bff;
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.status-paused .status-dot {
  background: #ffc107;
}

.status-completed .status-dot {
  background: #28a745;
}

.status-failed .status-dot {
  background: #dc3545;
}

.status-cancelled .status-dot {
  background: #6c757d;
}

.status-draft {
  color: #6c757d;
}

.status-ready {
  color: #17a2b8;
}

.status-running {
  color: #007bff;
}

.status-paused {
  color: #ffc107;
}

.status-completed {
  color: #28a745;
}

.status-failed {
  color: #dc3545;
}

.status-cancelled {
  color: #6c757d;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.action-btn {
  width: 32px;
  height: 32px;
  border: 1px solid #e9ecef;
  background: #fff;
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.action-btn:hover {
  border-color: #1976d2;
  background: #f0f7ff;
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.execute-btn {
  background: #28a745;
  border-color: #28a745;
  color: #fff;
}

.execute-btn:hover {
  background: #218838;
  border-color: #218838;
}

.delete-btn:hover {
  border-color: #dc3545;
  background: #fff5f5;
}

.more-actions {
  display: flex;
  gap: 4px;
}

@media (max-width: 768px) {
  .workflow-card {
    padding: 12px;
  }

  .workflow-icon {
    width: 40px;
    height: 40px;
  }

  .icon-fallback {
    font-size: 20px;
  }

  .workflow-name {
    font-size: 14px;
  }
}
</style>
