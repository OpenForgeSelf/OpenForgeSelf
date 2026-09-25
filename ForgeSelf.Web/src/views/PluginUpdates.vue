<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import { useOpenPage } from '@/composables/useOpenPage'

const pluginStore = usePluginStore()
const { openPage } = useOpenPage()

const updatingPluginIds = ref<Set<string>>(new Set())

async function handleUpdate(pluginId: string): Promise<void> {
  updatingPluginIds.value.add(pluginId)
  try {
    await pluginStore.updatePlugin(pluginId)
  } catch (e) {
    console.error('更新插件失败:', e)
  } finally {
    updatingPluginIds.value.delete(pluginId)
  }
}

async function handleUpdateAll(): Promise<void> {
  const ids = pluginStore.updates.map(u => u.pluginId)
  for (const id of ids) {
    updatingPluginIds.value.add(id)
    try {
      await pluginStore.updatePlugin(id)
    } catch (e) {
      console.error(`更新插件 ${id} 失败:`, e)
    } finally {
      updatingPluginIds.value.delete(id)
    }
  }
}

function handleViewDetail(pluginId: string): void {
  openPage(`/plugins/${pluginId}`, '插件管理')
}

function handleRefresh(): void {
  pluginStore.checkForUpdates()
}

onMounted(() => {
  pluginStore.checkForUpdates()
})
</script>

<template>
  <div class="plugin-updates-page">
    <header class="page-header">
      <div>
        <h1 class="page-title">插件更新</h1>
        <p class="page-subtitle">
          {{ pluginStore.updates.length > 0 
            ? `发现 ${pluginStore.updates.length} 个可更新插件` 
            : '所有插件都是最新版本' }}
        </p>
      </div>
      <div class="header-actions">
        <button class="action-btn refresh-btn" :disabled="pluginStore.isLoading" @click="handleRefresh">
          🔄 刷新
        </button>
        <button
          v-if="pluginStore.updates.length > 0"
          class="action-btn update-all-btn"
          :disabled="pluginStore.isLoading || updatingPluginIds.size > 0"
          @click="handleUpdateAll"
        >
          全部更新
        </button>
      </div>
    </header>

    <div v-if="pluginStore.isLoading" class="loading-state">
      <div class="loading-spinner" />
      <p>正在检查更新...</p>
    </div>

    <div v-else-if="pluginStore.error" class="error-state">
      <div class="error-icon">⚠️</div>
      <p>{{ pluginStore.error }}</p>
      <button class="retry-btn" @click="handleRefresh">重试</button>
    </div>

    <div v-else-if="pluginStore.updates.length === 0" class="empty-state">
      <div class="empty-icon">🎉</div>
      <h3>所有插件都是最新版本</h3>
      <p>你目前使用的都是最新版本的插件</p>
    </div>

    <div v-else class="updates-list">
      <div
        v-for="update in pluginStore.updates"
        :key="update.pluginId"
        class="update-card"
      >
        <div class="card-main">
          <div class="plugin-icon">
            <span class="icon-fallback">📦</span>
          </div>
          <div class="plugin-info" @click="handleViewDetail(update.pluginId)">
            <h3 class="plugin-name">{{ update.pluginName }}</h3>
            <div class="version-info">
              <span class="current-version">v{{ update.currentVersion }}</span>
              <span class="arrow">→</span>
              <span class="new-version">v{{ update.latestVersion }}</span>
            </div>
          </div>
        </div>
        <div class="card-actions">
          <button
            class="update-btn"
            :disabled="updatingPluginIds.has(update.pluginId)"
            @click="handleUpdate(update.pluginId)"
          >
            <span v-if="updatingPluginIds.has(update.pluginId)" class="btn-spinner" />
            {{ updatingPluginIds.has(update.pluginId) ? '更新中...' : '更新' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.plugin-updates-page {
  padding: 24px;
  min-height: 100%;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 4px 0;
}

.page-subtitle {
  font-size: 14px;
  color: var(--el-text-color-secondary);
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.action-btn {
  padding: 10px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.refresh-btn {
  background: var(--el-bg-color-page);
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
}

.refresh-btn:hover:not(:disabled) {
  background: var(--el-fill-color);
}

.update-all-btn {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.update-all-btn:hover:not(:disabled) {
  background: var(--el-color-primary-light-3);
}

.action-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.loading-state,
.error-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--el-border-color-light);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.loading-state p,
.empty-state p {
  color: var(--el-text-color-secondary);
  margin: 0;
}

.error-state p {
  color: var(--el-color-danger);
  margin: 0 0 16px 0;
}

.error-icon,
.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.empty-state h3 {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 8px 0;
}

.retry-btn {
  padding: 8px 20px;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  border: none;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
}

.updates-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.update-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  transition: all 0.2s;
}

.update-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 2px 8px var(--primary-light);
}

.card-main {
  display: flex;
  align-items: center;
  gap: 16px;
}

.plugin-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  background: var(--el-color-primary-light-9);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.icon-fallback {
  font-size: 28px;
}

.plugin-info {
  cursor: pointer;
}

.plugin-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 6px 0;
}

.version-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.current-version {
  color: var(--el-text-color-secondary);
}

.arrow {
  color: var(--el-text-color-secondary);
}

.new-version {
  color: var(--el-color-success);
  font-weight: 500;
}

.update-btn {
  padding: 10px 24px;
  background: #ffc107;
  color: var(--el-text-color-primary);
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 8px;
}

.update-btn:hover:not(:disabled) {
  background: #e0a800;
}

.update-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@media (max-width: 640px) {
  .plugin-updates-page {
    padding: 16px;
  }

  .page-header {
    flex-direction: column;
  }

  .update-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }

  .card-actions {
    width: 100%;
  }

  .update-btn {
    width: 100%;
    justify-content: center;
  }
}
</style>