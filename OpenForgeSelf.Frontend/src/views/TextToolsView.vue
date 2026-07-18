<script setup lang="ts">
import { useTextToolsStore } from '@/stores/textTools'
import type { TextToolTab } from '@/types/textTools'
import ToolTabs from '@/components/texttools/ToolTabs.vue'
import TextInput from '@/components/texttools/TextInput.vue'
import TextOutput from '@/components/texttools/TextOutput.vue'
import FormatterPanel from '@/components/texttools/FormatterPanel.vue'
import EncodingPanel from '@/components/texttools/EncodingPanel.vue'
import HashPanel from '@/components/texttools/HashPanel.vue'
import StatsPanel from '@/components/texttools/StatsPanel.vue'

const store = useTextToolsStore()

function handleTabChange(tab: TextToolTab): void {
  store.setTab(tab)
}
</script>

<template>
  <div class="text-tools-view">
    <header class="view-header">
      <h1 class="view-title">
        <span class="title-icon">🛠️</span>
        文本工具箱
      </h1>
      <p class="view-subtitle">格式化、编解码、哈希计算、文本统计等常用工具</p>
    </header>

    <ToolTabs :active-tab="store.currentTab" @change="handleTabChange" />

    <div v-if="store.error" class="error-banner" role="alert">
      <span class="error-icon">⚠️</span>
      <span class="error-message">{{ store.error }}</span>
      <button class="error-close" aria-label="关闭错误提示" @click="store.clearError()">
        ✕
      </button>
    </div>

    <div v-if="store.currentTab === 'stats'" class="stats-only-view">
      <StatsPanel />
      <div class="text-input-wrapper">
        <TextInput v-model="store.inputText" @clear="store.clearInput()" />
      </div>
    </div>

    <div v-else class="tool-content">
      <div class="panels-section">
        <FormatterPanel v-if="store.currentTab === 'formatter'" />
        <EncodingPanel v-else-if="store.currentTab === 'encoding'" />
        <HashPanel v-else-if="store.currentTab === 'hash'" />
      </div>

      <div class="editors-section">
        <div class="editor-panel">
          <TextInput v-model="store.inputText" @clear="store.clearInput()" />
        </div>
        <div class="editor-panel">
          <TextOutput v-model="store.outputText" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.text-tools-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

.view-header {
  flex-shrink: 0;
}

.view-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.title-icon {
  font-size: 28px;
}

.view-subtitle {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  background: rgba(180, 83, 9, 0.08);
  border: 1px solid rgba(180, 83, 9, 0.2);
  border-radius: var(--radius-md, 8px);
  color: var(--warning-color);
  flex-shrink: 0;
}

.error-icon {
  font-size: 18px;
  flex-shrink: 0;
}

.error-message {
  flex: 1;
  font-size: 14px;
}

.error-close {
  background: none;
  border: none;
  font-size: 16px;
  color: var(--warning-color);
  cursor: pointer;
  padding: 4px;
  line-height: 1;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.error-close:hover {
  opacity: 1;
}

.stats-only-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

.text-input-wrapper {
  flex: 1;
  min-height: 200px;
}

.tool-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

.panels-section {
  flex-shrink: 0;
}

.editors-section {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

.editor-panel {
  min-height: 0;
  display: flex;
  flex-direction: column;
}

@media (max-width: 900px) {
  .editors-section {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr 1fr;
  }
}

@media (max-width: 640px) {
  .text-tools-view {
    padding: 12px;
    gap: 12px;
  }

  .view-title {
    font-size: 20px;
  }

  .view-subtitle {
    font-size: 13px;
  }
}
</style>