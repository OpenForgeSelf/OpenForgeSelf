<script setup lang="ts">
import type { ChatSessionSummary } from '@/types/chatRecords'

defineProps<{
  records: ChatSessionSummary[]
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

function getSourceBadgeClass(source: string): string {
  switch (source) {
    case 'App':
      return 'badge-app'
    case 'Proxy':
      return 'badge-proxy'
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
          <th class="col-session">会话键</th>
          <th class="col-source">来源</th>
          <th class="col-style">Style</th>
          <th class="col-model">Model</th>
          <th class="col-summary">首条消息</th>
          <th class="col-turns">轮次</th>
          <th class="col-messages">消息数</th>
          <th class="col-time">更新时间</th>
          <th class="col-action">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="record in records" :key="record.id" class="record-row">
          <td class="col-id">{{ record.id }}</td>
          <td class="col-session">
            <span class="session-id" :title="record.sessionKey">{{ record.sessionKey.slice(0, 8) }}...</span>
          </td>
          <td class="col-source">
            <span class="source-badge" :class="getSourceBadgeClass(record.source)">
              {{ record.source }}
            </span>
          </td>
          <td class="col-style">
            <span class="style-badge" :class="getStyleBadgeClass(record.style || '')">
              {{ record.style || '—' }}
            </span>
          </td>
          <td class="col-model">{{ record.model || '—' }}</td>
          <td class="col-summary">
            <span class="summary-text" :title="record.firstUserMsg ?? ''">{{ record.firstUserMsg || '—' }}</span>
          </td>
          <td class="col-turns">{{ record.requestCount }}</td>
          <td class="col-messages">{{ record.messageCount }}</td>
          <td class="col-time">{{ formatDate(record.updatedTime) }}</td>
          <td class="col-action">
            <button class="detail-btn" type="button" @click="emit('view-detail', record.id)">
              查看详情
            </button>
          </td>
        </tr>
        <tr v-if="records.length === 0 && !loading">
          <td colspan="10" class="empty-row">暂无数据</td>
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
          type="button"
          :disabled="page <= 1"
          @click="emit('page-change', page - 1)"
        >
          上一页
        </button>
        <button
          class="page-btn"
          type="button"
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
  background: color-mix(in srgb, var(--el-bg-color) 80%, transparent);
  z-index: 10;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid var(--el-border-color);
  border-top-color: var(--el-color-primary);
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
  color: var(--el-text-color-primary);
  background: var(--el-fill-color-light);
  border-bottom: 2px solid var(--el-border-color);
  white-space: nowrap;
}

.records-table td {
  padding: 12px 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-primary);
}

.record-row:hover {
  background: var(--el-fill-color-light);
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
  color: var(--el-text-color-secondary);
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
  color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--el-color-primary) 30%, transparent);
}

.badge-responses {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--el-color-success) 30%, transparent);
}

.badge-anthropic {
  color: var(--el-color-warning);
  background: color-mix(in srgb, var(--el-color-warning) 14%, transparent);
  border: 1px solid color-mix(in srgb, var(--el-color-warning) 32%, transparent);
}

.source-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.badge-app {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--el-color-success) 30%, transparent);
}

.badge-proxy {
  color: var(--el-color-info);
  background: color-mix(in srgb, var(--el-color-info) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--el-color-info) 30%, transparent);
}

.col-summary {
  max-width: 220px;
}

.summary-text {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-regular);
}

.col-model {
  max-width: 150px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.col-messages, .col-turns, .col-source {
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
  border: 1px solid var(--el-color-primary);
  background: var(--el-bg-color);
  color: var(--el-color-primary);
  font-size: 13px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.detail-btn:hover {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.empty-row {
  text-align: center;
  color: var(--el-text-color-secondary);
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
  color: var(--el-text-color-secondary);
}

.pagination-controls {
  display: flex;
  gap: 8px;
}

.page-btn {
  padding: 8px 16px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 14px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.page-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
