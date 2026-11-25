<template>
  <div class="code-snippet-card" :class="{ 'is-favorite': snippet.isFavorite }">
    <div class="snippet-header">
      <h4 class="snippet-title" :title="snippet.title">{{ snippet.title }}</h4>
      <button class="favorite-btn" @click.stop="toggleFavorite">
        <i :class="snippet.isFavorite ? 'fa-solid fa-star' : 'fa-regular fa-star'" />
      </button>
    </div>

    <p v-if="snippet.description" class="snippet-description" :title="snippet.description">
      {{ snippet.description }}
    </p>

    <div class="snippet-meta">
      <span class="language-tag">{{ snippet.language }}</span>
      <span v-if="snippet.category" class="category-tag">{{ snippet.category }}</span>
    </div>

    <div v-if="snippet.tags && snippet.tags.length > 0" class="snippet-tags">
      <span v-for="tag in snippet.tags.slice(0, 3)" :key="tag" class="tag">#{{ tag }}</span>
    </div>

    <div class="snippet-footer">
      <span class="usage-count">
        <i class="fa-solid fa-play" />
        {{ snippet.usageCount }} 次使用
      </span>
      <span class="source-badge" :class="'source-' + sourceName(props.snippet.source).toLowerCase()">
        {{ getSourceText(props.snippet.source) }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { CodeSnippet, CodeSnippetSource } from '@/types/codeSnippet'

const props = defineProps<{
  snippet: CodeSnippet
}>()

const emit = defineEmits<{
  toggleFavorite: [id: number, isFavorite: boolean]
}>()

function getSourceText(source: CodeSnippetSource): string {
  const sourceMap: Record<CodeSnippetSource, string> = {
    Manual: '手动',
    Script: '脚本',
    AIGenerated: 'AI生成'
  }
  return sourceMap[source] || source
}

/** 后端 CodeSnippetSource 枚举序列化为数字（0=Manual/1=Script/2=AIGenerated），映射为字符串名称避免 .toLowerCase() 崩溃 */
const SOURCE_NAMES: Record<number, string> = {
  0: 'Manual',
  1: 'Script',
  2: 'AIGenerated'
}

function sourceName(source: CodeSnippetSource | number): string {
  if (typeof source === 'string') return source
  return SOURCE_NAMES[source] ?? 'Manual'
}

function toggleFavorite() {
  emit('toggleFavorite', props.snippet.id, !props.snippet.isFavorite)
}
</script>

<style scoped>
.code-snippet-card {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.code-snippet-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: var(--el-box-shadow-light);
}

.code-snippet-card.is-favorite {
  border-left: 3px solid var(--el-color-primary);
}

.snippet-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

.snippet-title {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.favorite-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--el-color-primary);
  font-size: 14px;
  padding: 2px;
}

.favorite-btn:hover {
  transform: scale(1.2);
}

.snippet-description {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-regular);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.snippet-meta {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.language-tag {
  display: inline-block;
  padding: 2px 8px;
  background: var(--el-color-primary-light-3);
  color: var(--el-color-primary);
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
}

.category-tag {
  display: inline-block;
  padding: 2px 8px;
  background: var(--el-bg-color-page);
  color: var(--el-text-color-regular);
  border-radius: 4px;
  font-size: 11px;
}

.snippet-tags {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.tag {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.snippet-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: auto;
  padding-top: 4px;
  border-top: 1px solid var(--el-border-color-light);
}

.usage-count {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.source-badge {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 3px;
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
</style>
