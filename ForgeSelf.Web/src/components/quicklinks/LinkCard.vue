<script setup lang="ts">
import { ref } from 'vue'
import type { QuickLink } from '@/types/quickLinks'
import { useQuickLinksStore } from '@/stores/quickLinks'

const props = defineProps<{
  link: QuickLink
  draggable?: boolean
}>()

const emit = defineEmits<{
  (e: 'edit', link: QuickLink): void
  (e: 'delete', link: QuickLink): void
  (e: 'dragstart', link: QuickLink): void
  (e: 'dragend'): void
}>()

const quickLinksStore = useQuickLinksStore()
const isHovered = ref(false)

function handleClick(): void {
  window.open(props.link.url, '_blank', 'noopener,noreferrer')
  quickLinksStore.recordClick(props.link.id)
}

function handleEdit(event: Event): void {
  event.stopPropagation()
  emit('edit', props.link)
}

function handleDelete(event: Event): void {
  event.stopPropagation()
  emit('delete', props.link)
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    handleClick()
  }
}

function handleDragStart(event: DragEvent): void {
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', props.link.id)
  }
  emit('dragstart', props.link)
}

function handleDragEnd(): void {
  emit('dragend')
}
</script>

<template>
  <div
    class="link-card"
    role="button"
    tabindex="0"
    :draggable="draggable"
    :aria-label="`链接 ${link.name}，点击在新标签页打开`"
    @click="handleClick"
    @keydown="handleKeydown"
    @mouseenter="isHovered = true"
    @mouseleave="isHovered = false"
    @dragstart="handleDragStart"
    @dragend="handleDragEnd"
  >
    <div class="card-header">
      <div class="link-icon">
        <span v-if="link.icon" class="icon-content">{{ link.icon }}</span>
        <span v-else class="icon-fallback">🔗</span>
      </div>
      <div class="link-info">
        <h3 class="link-name">{{ link.name }}</h3>
        <p class="link-url" :title="link.url">{{ link.url }}</p>
      </div>
      <div v-show="isHovered" class="card-actions">
        <button
          class="action-btn edit-btn"
          :aria-label="`编辑 ${link.name}`"
          @click="handleEdit"
        >
          ✏️
        </button>
        <button
          class="action-btn delete-btn"
          :aria-label="`删除 ${link.name}`"
          @click="handleDelete"
        >
          🗑️
        </button>
      </div>
    </div>

    <div class="card-body">
      <p class="link-description">{{ link.description }}</p>
    </div>

    <div class="card-footer">
      <span class="click-count">
        <span class="click-icon">👆</span>
        {{ link.clickCount }} 次点击
      </span>
    </div>
  </div>
</template>

<style scoped>
.link-card {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.25s ease;
  display: flex;
  flex-direction: column;
  gap: 12px;
  outline: none;
  user-select: none;
}

.link-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 4px 12px var(--primary-light);
  transform: translateY(-2px);
}

.link-card:focus-visible {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.link-card:active {
  transform: translateY(0);
}

.card-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.link-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background: var(--el-color-primary-light-9);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 24px;
}

.icon-fallback {
  font-size: 24px;
}

.link-info {
  flex: 1;
  min-width: 0;
}

.link-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 4px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.link-url {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.action-btn {
  width: 28px;
  height: 28px;
  border: none;
  background: var(--el-fill-color-light);
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.action-btn:hover {
  background: var(--el-fill-color);
}

.edit-btn:hover {
  background: #fff3cd;
}

.delete-btn:hover {
  background: #f8d7da;
}

.card-body {
  flex: 1;
}

.link-description {
  font-size: 13px;
  color: var(--el-text-color-regular);
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
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid var(--el-border-color-light);
}

.click-count {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.click-icon {
  font-size: 12px;
}

@media (max-width: 768px) {
  .link-card {
    padding: 12px;
  }

  .link-icon {
    width: 40px;
    height: 40px;
    font-size: 20px;
  }

  .icon-fallback {
    font-size: 20px;
  }

  .link-name {
    font-size: 14px;
  }
}
</style>
