<script setup lang="ts">
import type { TextToolTab } from '@/types/textTools'

interface TabItem {
  key: TextToolTab
  label: string
  icon: string
}

defineProps<{
  activeTab: TextToolTab
}>()

const emit = defineEmits<{
  (e: 'change', tab: TextToolTab): void
}>()

const tabs: TabItem[] = [
  { key: 'formatter', label: '格式化', icon: '📝' },
  { key: 'encoding', label: '编解码', icon: '🔐' },
  { key: 'hash', label: '哈希', icon: '🔒' },
  { key: 'stats', label: '统计', icon: '📊' }
]
</script>

<template>
  <div class="tool-tabs" role="tablist">
    <button
      v-for="tab in tabs"
      :key="tab.key"
      class="tab-item"
      :class="{ active: activeTab === tab.key }"
      role="tab"
      :aria-selected="activeTab === tab.key"
      @click="emit('change', tab.key)"
    >
      <span class="tab-icon">{{ tab.icon }}</span>
      <span class="tab-label">{{ tab.label }}</span>
    </button>
  </div>
</template>

<style scoped>
.tool-tabs {
  display: flex;
  gap: 4px;
  padding: 8px;
  background-color: var(--bg-muted);
  border-radius: 8px;
  overflow-x: auto;
}

.tab-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  border: none;
  background: transparent;
  border-radius: 6px;
  color: var(--text-secondary);
  font-size: 14px;
  white-space: nowrap;
  transition: all 0.2s ease;
  cursor: pointer;
}

.tab-item:hover {
  background-color: var(--bg-hover);
  color: var(--text-primary);
}

.tab-item.active {
  background-color: var(--bg-card);
  color: var(--primary-color);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  font-weight: 500;
}

.tab-icon {
  font-size: 16px;
}

.tab-label {
  font-size: 14px;
}

@media (max-width: 640px) {
  .tab-label {
    display: none;
  }

  .tab-item {
    padding: 10px 12px;
  }
}
</style>
