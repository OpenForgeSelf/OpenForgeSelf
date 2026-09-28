<script setup lang="ts">
import { useFileToolsStore } from '../stores/fileTools'
import { fileToolsApi } from '../services/fileToolsApi'

const store = useFileToolsStore()

// 图表调色板：运行时读取官方 --el-color-*（canvas 不解析 CSS 变量，故取计算值），零自定义 token
const TYPE_COLOR_VARS = [
  '--el-color-primary',
  '--el-color-success',
  '--el-color-warning',
  '--el-color-danger',
  '--el-color-info',
  '--el-color-primary-light-3',
  '--el-color-success-light-3',
  '--el-color-info-light-3'
]

function getTypeColor(index: number): string {
  const name = TYPE_COLOR_VARS[index % TYPE_COLOR_VARS.length]
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || '#888888'
}

function setDemoDirectory(): void {
  store.setSelectedDirectory('C:/Users/User/Documents')
  store.loadStats()
}
</script>

<template>
  <div class="stats-panel">
    <div class="directory-bar">
      <div class="directory-input">
        <label class="form-label">目录路径</label>
        <div class="input-group">
          <input
            type="text"
            class="form-input"
            :value="store.selectedDirectory"
            placeholder="输入或选择目录路径"
            @input="(e) => store.setSelectedDirectory((e.target as HTMLInputElement).value)"
          />
          <button class="btn btn-sm btn-secondary" @click="setDemoDirectory">
            示例
          </button>
          <button
            class="btn btn-sm btn-primary"
            :disabled="!store.selectedDirectory || store.isProcessing"
            @click="store.loadStats()"
          >
            🔄 刷新
          </button>
        </div>
      </div>
    </div>

    <div v-if="store.isProcessing" class="loading-state">
      <div class="loading-spinner">⏳</div>
      <div class="loading-text">正在统计中...</div>
    </div>

    <div v-else-if="!store.statsData" class="empty-state">
      <div class="empty-icon">📊</div>
      <div class="empty-text">选择目录后点击刷新查看统计</div>
    </div>

    <div v-else class="stats-content">
      <div class="overview-cards">
        <div class="stat-card">
          <div class="card-icon">📄</div>
          <div class="card-info">
            <div class="card-value">{{ store.statsData.fileCount }}</div>
            <div class="card-label">文件总数</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="card-icon">📁</div>
          <div class="card-info">
            <div class="card-value">{{ store.statsData.folderCount }}</div>
            <div class="card-label">文件夹数</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="card-icon">💾</div>
          <div class="card-info">
            <div class="card-value">{{ fileToolsApi.formatFileSize(store.statsData.totalSize) }}</div>
            <div class="card-label">总大小</div>
          </div>
        </div>
      </div>

      <div class="stats-grid">
        <div class="stats-section">
          <h3 class="section-title">📊 文件类型分布</h3>
          <div class="type-breakdown">
            <div
              v-for="(item, index) in store.statsData.typeBreakdown"
              :key="item.extension"
              class="type-item"
            >
              <div class="type-header">
                <span class="type-name">
                  <span class="type-dot" :style="{ backgroundColor: getTypeColor(index) }" />
                  {{ item.extension }}
                </span>
                <span class="type-stats">
                  {{ item.count }} 个 · {{ fileToolsApi.formatFileSize(item.size) }}
                </span>
              </div>
              <div class="type-bar">
                <div
                  class="type-bar-fill"
                  :style="{
                    width: item.percentage + '%',
                    backgroundColor: getTypeColor(index)
                  }"
                />
              </div>
              <div class="type-percentage">{{ item.percentage.toFixed(1) }}%</div>
            </div>
          </div>
        </div>

        <div class="stats-section">
          <h3 class="section-title">🏆 大文件排行榜</h3>
          <div class="large-files-list">
            <div
              v-for="(file, index) in store.statsData.largeFiles"
              :key="index"
              class="large-file-item"
            >
              <div class="file-rank">{{ index + 1 }}</div>
              <div class="file-info">
                <div class="file-name" :title="file.fileName">{{ file.fileName }}</div>
                <div class="file-path" :title="file.filePath">{{ file.filePath }}</div>
              </div>
              <div class="file-size">{{ fileToolsApi.formatFileSize(file.size) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.stats-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}

.directory-bar {
  flex-shrink: 0;
}

.directory-input {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-regular);
}

.input-group {
  display: flex;
  gap: 8px;
}

.form-input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 13px;
  color: var(--el-text-color-regular);
  background-color: var(--el-bg-color);
}

.form-input:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.btn-primary:hover:not(:disabled) {
  background-color: var(--el-color-primary-light-3);
}

.btn-secondary {
  background-color: var(--el-text-color-secondary);
  color: var(--el-color-white);
}

.btn-secondary:hover:not(:disabled) {
  opacity: 0.85;
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.loading-state,
.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-secondary);
}

.loading-spinner,
.empty-icon {
  font-size: 64px;
  margin-bottom: 12px;
}

.loading-text,
.empty-text {
  font-size: 14px;
}

.stats-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.overview-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  flex-shrink: 0;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  background-color: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
}

.card-icon {
  font-size: 36px;
  flex-shrink: 0;
}

.card-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.card-value {
  font-size: 20px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  line-height: 1.2;
}

.card-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.stats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.stats-section {
  display: flex;
  flex-direction: column;
  background-color: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 16px;
  min-height: 0;
  overflow: hidden;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 16px 0;
  flex-shrink: 0;
}

.type-breakdown {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-right: 4px;
}

.type-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.type-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
}

.type-name {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.type-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.type-stats {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.type-bar {
  height: 6px;
  background-color: var(--el-fill-color-light);
  border-radius: 3px;
  overflow: hidden;
}

.type-bar-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.type-percentage {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  text-align: right;
}

.large-files-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  padding-right: 4px;
}

.large-file-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--el-border-color-light);
}

.large-file-item:last-child {
  border-bottom: none;
}

.file-rank {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  background-color: var(--el-fill-color-light);
  border-radius: 50%;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.large-file-item:nth-child(1) .file-rank {
  background-color: var(--el-color-warning);
  color: var(--el-color-warning-dark-2);
}

.large-file-item:nth-child(2) .file-rank {
  background-color: var(--el-border-color-darker);
  color: var(--el-text-color-regular);
}

.large-file-item:nth-child(3) .file-rank {
  background-color: var(--el-color-warning-dark-2);
  color: var(--el-color-white);
}

.file-info {
  flex: 1;
  min-width: 0;
}

.file-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-path {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-top: 2px;
}

.file-size {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-regular);
  flex-shrink: 0;
}

@media (max-width: 900px) {
  .overview-cards {
    grid-template-columns: 1fr;
  }

  .stats-grid {
    grid-template-columns: 1fr;
    grid-template-rows: auto auto;
  }
}

@media (max-width: 640px) {
  .card-value {
    font-size: 18px;
  }

  .card-icon {
    font-size: 28px;
  }

  .section-title {
    font-size: 14px;
  }
}
</style>
