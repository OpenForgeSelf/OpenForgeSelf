<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import { useOpenPage } from '@/composables/useOpenPage'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'

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
    <PageHead
      eyebrow="Plugin Updates · 插件更新"
      :title="pluginStore.updates.length > 0 ? `发现 ${pluginStore.updates.length} 个可更新插件` : '插件更新'"
    >
      <template #actions>
        <button class="action-btn refresh-btn" :disabled="pluginStore.isLoading" @click="handleRefresh">
          刷新
        </button>
        <button
          v-if="pluginStore.updates.length > 0"
          class="action-btn update-all-btn"
          :disabled="pluginStore.isLoading || updatingPluginIds.size > 0"
          @click="handleUpdateAll"
        >
          全部更新
        </button>
      </template>
    </PageHead>

    <div v-if="pluginStore.isLoading" class="loading-state">
      <div class="loading-spinner" />
      <p>正在检查更新...</p>
    </div>

    <EmptyState
      v-else-if="pluginStore.error"
      icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10' /><line x1='12' y1='8' x2='12' y2='12' /><line x1='12' y1='16' x2='12.01' y2='16' /></svg>"
      title="检查更新失败"
      :sub="pluginStore.error"
    >
      <template #action>
        <button class="retry-btn" @click="handleRefresh">重试</button>
      </template>
    </EmptyState>

    <EmptyState
      v-else-if="pluginStore.updates.length === 0"
      icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14' /><polyline points='22 4 12 14.01 9 11.01' /></svg>"
      title="所有插件都是最新版本"
      sub="你目前使用的都是最新版本的插件"
    />

    <div v-else class="updates-list">
      <div
        v-for="update in pluginStore.updates"
        :key="update.pluginId"
        class="update-card glass"
      >
        <div class="card-main">
          <div class="plugin-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
              <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
              <line x1="12" y1="22.08" x2="12" y2="12" />
            </svg>
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
  max-width: 1200px;
  margin: 0 auto;
}

.action-btn {
  padding: 8px 16px;
  border: none;
  border-radius: var(--radius-md);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-instant) var(--ease-out);
  font-family: var(--font-sans-cn);
}

.refresh-btn {
  background: transparent;
  color: var(--fg-secondary);
  border: 1px solid var(--border-default);
}

.refresh-btn:hover:not(:disabled) {
  background: var(--paper-tint);
  transform: translateY(-1px);
}

.update-all-btn {
  background: var(--gold-400);
  color: var(--ink-950);
}

.update-all-btn:hover:not(:disabled) {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.loading-state {
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
  border: 3px solid var(--border-default);
  border-top-color: var(--gold-400);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.loading-state p {
  color: var(--fg-secondary);
  margin: 0;
}

.retry-btn {
  padding: 8px 20px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-md);
  font-size: 14px;
  cursor: pointer;
  font-family: var(--font-sans-cn);
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
  transition: border-color var(--dur-fast) var(--ease-out);
}

.update-card:hover {
  border-color: var(--border-brand);
}

.card-main {
  display: flex;
  align-items: center;
  gap: 16px;
}

.plugin-icon {
  width: 56px;
  height: 56px;
  border-radius: var(--radius-lg);
  background: var(--gold-tint);
  color: var(--gold-400);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.plugin-info {
  cursor: pointer;
}

.plugin-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 6px 0;
}

.version-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-family: var(--font-mono);
}

.current-version {
  color: var(--fg-tertiary);
}

.arrow {
  color: var(--fg-tertiary);
}

.new-version {
  color: var(--bronze-500);
  font-weight: 500;
}

.update-btn {
  padding: 10px 24px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-instant) var(--ease-out);
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--font-sans-cn);
}

.update-btn:hover:not(:disabled) {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.update-btn:active:not(:disabled) {
  transform: translateY(1px);
}

.update-btn:disabled {
  opacity: 0.5;
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
