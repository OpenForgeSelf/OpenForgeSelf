<template>
  <div class="code-snippet-viewer">
    <div class="viewer-header">
      <div class="snippet-info">
        <h3 class="snippet-title">{{ snippet?.title || '代码片段' }}</h3>
        <div class="snippet-meta">
          <span class="language-tag">{{ snippet?.language || 'unknown' }}</span>
          <span v-if="snippet?.category" class="category-tag">{{ snippet.category }}</span>
          <span class="usage-info">
            <i class="fa-solid fa-play" />
            {{ snippet?.usageCount || 0 }} 次使用
          </span>
        </div>
      </div>
      <div class="viewer-actions">
        <button class="action-btn favorite-btn" :class="{ active: snippet?.isFavorite }" @click="onToggleFavorite">
          <i :class="snippet?.isFavorite ? 'fa-solid fa-star' : 'fa-regular fa-star'" />
          {{ snippet?.isFavorite ? '已收藏' : '收藏' }}
        </button>
        <button class="action-btn copy-btn" @click="copyCode">
          <i class="fa-solid fa-copy" />
          {{ copied ? '已复制' : '复制代码' }}
        </button>
        <button class="action-btn edit-btn" @click="$emit('edit')">
          <i class="fa-solid fa-pen" />
          编辑
        </button>
        <button class="action-btn delete-btn" @click="onDelete">
          <i class="fa-solid fa-trash" />
          删除
        </button>
      </div>
    </div>

    <div v-if="snippet?.description" class="snippet-description">
      <p>{{ snippet.description }}</p>
    </div>

    <div v-if="snippet?.tags && snippet.tags.length > 0" class="snippet-tags">
      <span v-for="tag in snippet.tags" :key="tag" class="tag">#{{ tag }}</span>
    </div>

    <div class="code-container">
      <div class="code-header">
        <span class="code-language">{{ snippet?.language || 'code' }}</span>
        <span class="code-size">{{ codeSize }}</span>
      </div>
      <pre class="code-block"><code ref="codeElement">{{ snippet?.code || '' }}</code></pre>
    </div>

    <div class="snippet-footer">
      <div class="source-info">
        <span>来源: </span>
        <span class="source-badge" :class="'source-' + (snippet?.source || 'manual').toLowerCase()">
          {{ getSourceText(snippet?.source) }}
        </span>
      </div>
      <div class="time-info">
        <span>创建于: {{ formatDate(snippet?.createdAt) }}</span>
        <span v-if="snippet?.lastUsedAt">最后使用: {{ formatDate(snippet.lastUsedAt) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { CodeSnippet, CodeSnippetSource } from '@/types/codeSnippet'

const props = defineProps<{
  snippet: CodeSnippet | null
}>()

const emit = defineEmits<{
  edit: []
  delete: [id: number]
  toggleFavorite: [id: number, isFavorite: boolean]
}>()

const codeElement = ref<HTMLElement | null>(null)
const copied = ref(false)

const codeSize = computed(() => {
  if (!props.snippet?.code) return '0 B'
  const bytes = new Blob([props.snippet.code]).size
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
})

function getSourceText(source?: CodeSnippetSource): string {
  if (!source) return '未知'
  const sourceMap: Record<CodeSnippetSource, string> = {
    Manual: '手动创建',
    Script: '从脚本导入',
    AIGenerated: 'AI生成'
  }
  return sourceMap[source] || source
}

function formatDate(date?: Date): string {
  if (!date) return '未知'
  const d = new Date(date)
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

async function copyCode() {
  if (!props.snippet?.code) return

  try {
    await navigator.clipboard.writeText(props.snippet.code)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch (e) {
    console.error('复制失败:', e)
  }
}

function onToggleFavorite() {
  if (!props.snippet) return
  emit('toggleFavorite', props.snippet.id, !props.snippet.isFavorite)
}

function onDelete() {
  if (!props.snippet) return
  if (confirm(`确定要删除代码片段 "${props.snippet.title}" 吗？`)) {
    emit('delete', props.snippet.id)
  }
}
</script>

<style scoped>
.code-snippet-viewer {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: var(--el-bg-color);
}

.viewer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color);
  gap: 12px;
}

.snippet-info {
  flex: 1;
}

.snippet-title {
  margin: 0 0 8px 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.snippet-meta {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

.language-tag {
  display: inline-block;
  padding: 2px 10px;
  background: var(--el-color-primary-light-3);
  color: var(--el-color-primary);
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}

.category-tag {
  display: inline-block;
  padding: 2px 10px;
  background: var(--el-bg-color-page);
  color: var(--el-text-color-regular);
  border-radius: 4px;
  font-size: 12px;
}

.usage-info {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.viewer-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.action-btn {
  padding: 6px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  cursor: pointer;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s;
}

.action-btn:hover {
  background: var(--el-bg-color-page);
}

.action-btn.favorite-btn.active {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-primary-dark-2);
  border-color: var(--el-color-warning-light-7);
}

.action-btn.copy-btn:hover {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
  border-color: var(--el-color-success-light-7);
}

.action-btn.edit-btn:hover {
  background: var(--el-color-info-light-9);
  color: var(--el-color-primary);
  border-color: var(--el-color-info-light-7);
}

.action-btn.delete-btn:hover {
  background: var(--el-color-error-light-9);
  color: var(--el-color-error);
  border-color: var(--el-color-error-light-7);
}

.snippet-description {
  padding: 12px 16px;
  background: var(--el-bg-color-page);
  border-bottom: 1px solid var(--el-border-color);
}

.snippet-description p {
  margin: 0;
  font-size: 13px;
  color: var(--el-text-color-regular);
  line-height: 1.5;
}

.snippet-tags {
  padding: 10px 16px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--el-border-color);
}

.tag {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background: var(--el-bg-color-page);
  padding: 2px 8px;
  border-radius: 4px;
}

.code-container {
  flex: 1;
  overflow: auto;
  margin: 16px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
}

.code-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--el-bg-color-page);
  border-bottom: 1px solid var(--el-border-color);
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.code-block {
  margin: 0;
  padding: 16px;
  overflow: auto;
  background: var(--el-bg-color);
  flex: 1;
}

.code-block code {
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: var(--el-color-white);
  white-space: pre;
}

.snippet-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color);
  font-size: 12px;
  color: var(--el-text-color-secondary);
  flex-wrap: wrap;
  gap: 8px;
}

.source-info {
  display: flex;
  align-items: center;
  gap: 6px;
}

.source-badge {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
}

.source-manual {
  background: var(--el-color-info-light-9);
  color: var(--el-color-info);
}

.source-script {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
}

.source-aigenerated {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-primary-dark-2);
}

.time-info {
  display: flex;
  gap: 16px;
}
</style>
