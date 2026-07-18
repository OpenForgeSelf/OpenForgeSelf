<script setup lang="ts">
import { computed } from 'vue'
import type { Script, ScriptLanguage } from '@/types/scriptRunner'

const props = defineProps<{
  script: Script
}>()

const emit = defineEmits<{
  (e: 'click', script: Script): void
  (e: 'execute', script: Script): void
  (e: 'edit', script: Script): void
  (e: 'delete', script: Script): void
  (e: 'toggleFavorite', script: Script): void
}>()

const languageNames: Record<ScriptLanguage, string> = {
  powershell: 'PowerShell',
  python: 'Python',
  nodejs: 'Node.js',
  shell: 'Shell',
  cmd: 'CMD'
}

const languageColors: Record<ScriptLanguage, string> = {
  powershell: '#012456',
  python: '#3776AB',
  nodejs: '#339933',
  shell: '#4EAA25',
  cmd: '#4C4C4C'
}

const languageBgColors: Record<ScriptLanguage, string> = {
  powershell: '#E6F0FF',
  python: '#E8F4F8',
  nodejs: '#E8F5E9',
  shell: '#F1F8E9',
  cmd: '#F5F5F5'
}

const languageLabel = computed(() => languageNames[props.script.language])
const languageColor = computed(() => languageColors[props.script.language])
const languageBgColor = computed(() => languageBgColors[props.script.language])

function handleCardClick(): void {
  emit('click', props.script)
}

function handleExecuteClick(event: Event): void {
  event.stopPropagation()
  emit('execute', props.script)
}

function handleEditClick(event: Event): void {
  event.stopPropagation()
  emit('edit', props.script)
}

function handleDeleteClick(event: Event): void {
  event.stopPropagation()
  emit('delete', props.script)
}

function handleFavoriteClick(event: Event): void {
  event.stopPropagation()
  emit('toggleFavorite', props.script)
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
    class="script-card"
    role="button"
    tabindex="0"
    :aria-label="`脚本 ${script.name}，语言 ${languageLabel}`"
    @click="handleCardClick"
    @keydown="handleKeydown"
  >
    <div class="card-header">
      <div class="script-icon" :style="{ background: languageBgColor, color: languageColor }">
        {{ languageLabel.charAt(0) }}
      </div>
      <div class="script-basic">
        <h3 class="script-name">{{ script.name }}</h3>
        <span class="script-language" :style="{ background: languageBgColor, color: languageColor }">
          {{ languageLabel }}
        </span>
      </div>
      <button
        class="favorite-btn"
        :class="{ active: script.isFavorite }"
        :aria-label="script.isFavorite ? '取消收藏' : '收藏'"
        @click="handleFavoriteClick"
      >
        {{ script.isFavorite ? '⭐' : '☆' }}
      </button>
    </div>

    <div class="card-body">
      <p class="script-description">{{ script.description || '暂无描述' }}</p>
    </div>

    <div class="card-footer">
      <div class="script-meta">
        <span v-if="script.category" class="script-category">{{ script.category }}</span>
        <span class="script-usage">使用 {{ script.usageCount }} 次</span>
        <span v-if="script.parameters.length > 0" class="script-params">
          {{ script.parameters.length }} 个参数
        </span>
      </div>
      <div class="card-actions">
        <button
          class="action-btn execute-btn"
          aria-label="执行脚本"
          @click="handleExecuteClick"
        >
          ▶
        </button>
        <div class="more-actions">
          <button
            class="action-btn"
            aria-label="编辑脚本"
            @click="handleEditClick"
          >
            ✏️
          </button>
          <button
            class="action-btn delete-btn"
            aria-label="删除脚本"
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
.script-card {
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

.script-card:hover {
  border-color: #1976d2;
  box-shadow: 0 4px 12px rgba(25, 118, 210, 0.12);
  transform: translateY(-2px);
}

.script-card:focus-visible {
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.2);
}

.card-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.script-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  font-weight: 700;
  flex-shrink: 0;
}

.script-basic {
  flex: 1;
  min-width: 0;
}

.script-name {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 6px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.script-language {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 10px;
  font-weight: 500;
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

.script-description {
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

.script-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 12px;
  color: #6c757d;
  align-items: center;
}

.script-category {
  background: #f1f3f5;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 11px;
}

.script-params {
  background: #e8f4fd;
  color: #0369a1;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 11px;
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
  .script-card {
    padding: 12px;
  }

  .script-icon {
    width: 40px;
    height: 40px;
    font-size: 16px;
  }

  .script-name {
    font-size: 14px;
  }
}
</style>
