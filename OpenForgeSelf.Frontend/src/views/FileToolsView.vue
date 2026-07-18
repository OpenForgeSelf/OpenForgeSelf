<script setup lang="ts">
import { useFileToolsStore } from '@/stores/fileTools'
import type { FileToolTab } from '@/types/fileTools'
import { fileToolsApi } from '@/services/fileToolsApi'
import RenamePanel from '@/components/filetools/RenamePanel.vue'
import CleanupPanel from '@/components/filetools/CleanupPanel.vue'
import ArchivePanel from '@/components/filetools/ArchivePanel.vue'
import StatsPanel from '@/components/filetools/StatsPanel.vue'

const store = useFileToolsStore()

interface TabItem {
  key: FileToolTab
  label: string
  icon: string
}

const tabs: TabItem[] = [
  { key: 'rename', label: '批量重命名', icon: '📝' },
  { key: 'cleanup', label: '批量清理', icon: '🧹' },
  { key: 'archive', label: '压缩解压', icon: '📦' },
  { key: 'stats', label: '文件统计', icon: '📊' }
]

function handleTabChange(tab: FileToolTab): void {
  store.setTab(tab)
}
</script>

<template>
  <div class="file-tools-view">
    <header class="view-header">
      <h1 class="view-title">
        <span class="title-icon">📁</span>
        文件工具箱
      </h1>
      <p class="view-subtitle">批量重命名、文件清理、压缩解压、文件统计等实用工具</p>
    </header>

    <div class="tool-tabs" role="tablist">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        class="tab-item"
        :class="{ active: store.currentTab === tab.key }"
        role="tab"
        :aria-selected="store.currentTab === tab.key"
        @click="handleTabChange(tab.key)"
      >
        <span class="tab-icon">{{ tab.icon }}</span>
        <span class="tab-label">{{ tab.label }}</span>
      </button>
    </div>

    <div v-if="store.error" class="error-banner" role="alert">
      <span class="error-icon">⚠️</span>
      <span class="error-message">{{ store.error }}</span>
      <button class="error-close" aria-label="关闭错误提示" @click="store.clearError()">
        ✕
      </button>
    </div>

    <div v-if="store.isProcessing" class="progress-bar">
      <div class="progress-fill" :style="{ width: store.progress + '%' }" />
    </div>

    <div class="tool-content">
      <RenamePanel v-if="store.currentTab === 'rename'" />
      <CleanupPanel v-else-if="store.currentTab === 'cleanup'" />
      <ArchivePanel v-else-if="store.currentTab === 'archive'" />
      <StatsPanel v-else-if="store.currentTab === 'stats'" />
    </div>

    <footer class="status-bar">
      <div class="status-left">
        <span v-if="store.currentTab === 'rename'" class="status-item">
          已选文件: {{ store.selectedFileCount }} 个
        </span>
        <span v-else-if="store.currentTab === 'cleanup'" class="status-item">
          待清理: {{ store.cleanupPreview.filter(i => i.selected).length }} 个
          ({{ fileToolsApi.formatFileSize(store.totalCleanupSize) }})
        </span>
        <span v-else-if="store.currentTab === 'stats' && store.statsData" class="status-item">
          文件总数: {{ store.statsData.fileCount }} 个
        </span>
      </div>
      <div class="status-right">
        <span v-if="store.isProcessing" class="status-processing">处理中...</span>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.file-tools-view {
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

.tool-tabs {
  display: flex;
  gap: 4px;
  padding: 8px;
  background-color: var(--bg-muted);
  border-radius: 8px;
  overflow-x: auto;
  flex-shrink: 0;
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

.progress-bar {
  height: 4px;
  background-color: var(--border-color);
  border-radius: 2px;
  overflow: hidden;
  flex-shrink: 0;
}

.progress-fill {
  height: 100%;
  background-color: var(--primary-color);
  border-radius: 2px;
  transition: width 0.3s ease;
}

.tool-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background-color: var(--bg-secondary);
  border-top: 1px solid var(--border-color);
  border-radius: 0 0 8px 8px;
  flex-shrink: 0;
  font-size: 13px;
  color: var(--text-muted);
}

.status-left {
  display: flex;
  gap: 16px;
}

.status-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.status-processing {
  color: var(--primary-color);
  font-weight: 500;
}

@media (max-width: 640px) {
  .file-tools-view {
    padding: 12px;
    gap: 12px;
  }

  .view-title {
    font-size: 20px;
  }

  .view-subtitle {
    font-size: 13px;
  }

  .tab-label {
    display: none;
  }

  .tab-item {
    padding: 10px 12px;
  }
}
</style>