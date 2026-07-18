<script setup lang="ts">
import type { ChatRecordSummary } from '@/types/chatRecords'

defineProps<{
  records: ChatRecordSummary[]
  total: number
  page: number
  pageSize: number
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'view-detail', id: number): void
  (e: 'page-change', page: number): void
}>()

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function getStyleBadgeClass(style: string): string {
  switch (style) {
    case 'OpenAI_Chat':
      return 'badge-openai'
    case 'OpenAI_Responses':
      return 'badge-responses'
    case 'Anthropic_Messages':
      return 'badge-anthropic'
    default:
      return ''
  }
}
</script>

<template>
  <div class="chat-records-list">
    <div v-if="loading" class="loading-overlay">
      <div class="spinner" />
      <span>加载中...</span>
    </div>

    <table class="records-table">
      <thead>
        <tr>
          <th class="col-id">ID</th>
          <th class="col-session">SessionId</th>
          <th class="col-style">Style</th>
          <th class="col-model">Model</th>
          <th class="col-messages">消息数</th>
          <th class="col-tools">工具调用</th>
          <th class="col-time">时间</th>
          <th class="col-action">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="record in records" :key="record.id" class="record-row">
          <td class="col-id">{{ record.id }}</td>
          <td class="col-session">
            <span class="session-id" :title="record.sessionId">{{ record.sessionId.slice(0, 8) }}...</span>
          </td>
          <td class="col-style">
            <span class="style-badge" :class="getStyleBadgeClass(record.style)">
              {{ record.style }}
            </span>
          </td>
          <td class="col-model">{{ record.model }}</td>
          <td class="col-messages">{{ record.messageCount }}</td>
          <td class="col-tools">{{ record.toolCallCount }}</td>
          <td class="col-time">{{ formatDate(record.createdTime) }}</td>
          <td class="col-action">
            <button class="detail-btn" @click="emit('view-detail', record.id)">
              查看详情
            </button>
          </td>
        </tr>
        <tr v-if="records.length === 0 && !loading">
          <td colspan="8" class="empty-row">暂无数据</td>
        </tr>
      </tbody>
    </table>

    <div v-if="total > 0" class="pagination">
      <span class="pagination-info">
        共 {{ total }} 条记录，第 {{ page }} / {{ Math.ceil(total / pageSize) }} 页
      </span>
      <div class="pagination-controls">
        <button
          class="page-btn"
          :disabled="page <= 1"
          @click="emit('page-change', page - 1)"
        >
          上一页
        </button>
        <button
          class="page-btn"
          :disabled="page >= Math.ceil(total / pageSize)"
          @click="emit('page-change', page + 1)"
        >
          下一页
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.chat-records-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
  position: relative;
  min-height: 200px;
}

.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  background: rgba(255, 255, 255, 0.8);
  z-index: 10;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #dee2e6;
  border-top-color: #0d6efd;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.records-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.records-table th {
  padding: 12px 8px;
  text-align: left;
  font-weight: 600;
  color: #495057;
  background: #f8f9fa;
  border-bottom: 2px solid #dee2e6;
  white-space: nowrap;
}

.records-table td {
  padding: 12px 8px;
  border-bottom: 1px solid #dee2e6;
  color: #212529;
}

.record-row:hover {
  background: #f8f9fa;
}

.col-id {
  width: 60px;
}

.col-session {
  max-width: 120px;
}

.session-id {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: 'Monaco', 'Menlo', monospace;
  font-size: 12px;
}

.col-style {
  width: 150px;
}

.style-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.badge-openai {
  background: #f0fdf4;
  color: #16a34a;
  border: 1px solid #bbf7d0;
}

.badge-responses {
  background: #eff6ff;
  color: #2563eb;
  border: 1px solid #bfdbfe;
}

.badge-anthropic {
  background: #fef3c7;
  color: #d97706;
  border: 1px solid #fde68a;
}

.col-model {
  max-width: 150px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.col-messages, .col-tools {
  width: 80px;
  text-align: center;
}

.col-time {
  width: 160px;
  font-size: 13px;
}

.col-action {
  width: 100px;
  text-align: center;
}

.detail-btn {
  padding: 6px 12px;
  border: 1px solid #0d6efd;
  background: white;
  color: #0d6efd;
  font-size: 13px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.detail-btn:hover {
  background: #0d6efd;
  color: white;
}

.empty-row {
  text-align: center;
  color: #6c757d;
  padding: 40px 8px !important;
}

.pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0;
}

.pagination-info {
  font-size: 14px;
  color: #6c757d;
}

.pagination-controls {
  display: flex;
  gap: 8px;
}

.page-btn {
  padding: 8px 16px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  font-size: 14px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.page-btn:hover:not(:disabled) {
  background: #f8f9fa;
  border-color: #ced4da;
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
