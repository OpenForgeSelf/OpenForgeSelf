<script setup lang="ts">
import { ref } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'

const pluginStore = usePluginStore()

const activeTab = ref<'import' | 'export'>('import')
const isDragging = ref(false)
const selectedFile = ref<File | null>(null)
const isInstalling = ref(false)
const installResult = ref<{ success: boolean; message: string } | null>(null)
const selectedPluginIds = ref<Set<string>>(new Set())
const isPackaging = ref(false)

function handleDragOver(event: DragEvent): void {
  event.preventDefault()
  isDragging.value = true
}

function handleDragLeave(event: DragEvent): void {
  event.preventDefault()
  isDragging.value = false
}

function handleDrop(event: DragEvent): void {
  event.preventDefault()
  isDragging.value = false

  const files = event.dataTransfer?.files
  if (files && files.length > 0) {
    const file = files[0]
    if (file.name.endsWith('.forgeself-plugin') || file.name.endsWith('.zip')) {
      selectedFile.value = file
      installResult.value = null
    } else {
      installResult.value = { success: false, message: '请上传 .forgeself-plugin 或 .zip 格式的插件包' }
    }
  }
}

function handleFileSelect(event: Event): void {
  const target = event.target as HTMLInputElement
  const files = target.files
  if (files && files.length > 0) {
    selectedFile.value = files[0]
    installResult.value = null
  }
}

async function handleInstall(): Promise<void> {
  if (!selectedFile.value) return

  isInstalling.value = true
  installResult.value = null

  try {
    await pluginStore.installPlugin(selectedFile.value)
    installResult.value = { success: true, message: '插件安装成功！' }
    selectedFile.value = null
  } catch (e) {
    installResult.value = {
      success: false,
      message: e instanceof Error ? e.message : '安装失败'
    }
  } finally {
    isInstalling.value = false
  }
}

function togglePluginSelection(pluginId: string): void {
  if (selectedPluginIds.value.has(pluginId)) {
    selectedPluginIds.value.delete(pluginId)
  } else {
    selectedPluginIds.value.add(pluginId)
  }
  selectedPluginIds.value = new Set(selectedPluginIds.value)
}

function toggleSelectAll(): void {
  if (selectedPluginIds.value.size === pluginStore.plugins.length) {
    selectedPluginIds.value = new Set()
  } else {
    selectedPluginIds.value = new Set(pluginStore.plugins.map(p => p.id))
  }
}

async function handleExportSelected(): Promise<void> {
  const ids = Array.from(selectedPluginIds.value)
  if (ids.length === 0) return

  isPackaging.value = true
  try {
    for (const id of ids) {
      const blob = await pluginStore.packagePlugin(id)
      const plugin = pluginStore.plugins.find(p => p.id === id)
      const fileName = `${plugin?.name || id}-${plugin?.version || '1.0.0'}.forgeself-plugin`
      downloadBlob(blob, fileName)
    }
  } catch (e) {
    console.error('导出失败:', e)
  } finally {
    isPackaging.value = false
  }
}

async function handleExportAll(): Promise<void> {
  if (pluginStore.plugins.length === 0) return

  isPackaging.value = true
  try {
    for (const plugin of pluginStore.plugins) {
      const blob = await pluginStore.packagePlugin(plugin.id)
      const fileName = `${plugin.name}-${plugin.version}.forgeself-plugin`
      downloadBlob(blob, fileName)
    }
  } catch (e) {
    console.error('导出失败:', e)
  } finally {
    isPackaging.value = false
  }
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="plugin-import-export-page">
    <PageHead
      eyebrow="Plugin IO · 插件导入导出"
      title="插件导入导出"
    />

    <div class="tabs-nav">
      <button
        class="tab-btn"
        :class="{ active: activeTab === 'import' }"
        @click="activeTab = 'import'"
      >
        导入插件
      </button>
      <button
        class="tab-btn"
        :class="{ active: activeTab === 'export' }"
        @click="activeTab = 'export'"
      >
        导出插件
      </button>
    </div>

    <div class="tab-content glass">
      <div v-if="activeTab === 'import'" class="import-section">
        <div
          class="drop-zone"
          :class="{ dragging: isDragging, 'has-file': selectedFile }"
          @dragover="handleDragOver"
          @dragleave="handleDragLeave"
          @drop="handleDrop"
          @click="($refs.fileInput as HTMLInputElement)?.click()"
        >
          <input
            ref="fileInput"
            type="file"
            accept=".forgeself-plugin,.zip"
            style="display: none"
            @change="handleFileSelect"
          />
          <div v-if="!selectedFile" class="drop-content">
            <div class="drop-icon">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="M17 8 12 3 7 8" />
                <line x1="12" y1="3" x2="12" y2="15" />
              </svg>
            </div>
            <h3>拖拽插件包到此处</h3>
            <p>或点击选择文件</p>
            <p class="format-hint">支持 .forgeself-plugin 和 .zip 格式</p>
          </div>
          <div v-else class="file-info">
            <div class="file-icon">
              <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="M14 2 14 8 20 8" />
              </svg>
            </div>
            <div class="file-details">
              <span class="file-name">{{ selectedFile.name }}</span>
              <span class="file-size">{{ (selectedFile.size / 1024).toFixed(1) }} KB</span>
            </div>
            <button
              class="remove-btn"
              @click.stop="selectedFile = null; installResult = null"
            >
              ×
            </button>
          </div>
        </div>

        <div v-if="installResult" class="install-result" :class="{ success: installResult.success, error: !installResult.success }">
          {{ installResult.message }}
        </div>

        <button
          class="install-btn"
          :disabled="!selectedFile || isInstalling"
          @click="handleInstall"
        >
          <span v-if="isInstalling" class="btn-spinner" />
          {{ isInstalling ? '安装中...' : '安装插件' }}
        </button>
      </div>

      <div v-else class="export-section">
        <div class="export-toolbar">
          <label class="select-all">
            <input
              type="checkbox"
              :checked="selectedPluginIds.size === pluginStore.plugins.length && pluginStore.plugins.length > 0"
              @change="toggleSelectAll"
            />
            全选 ({{ selectedPluginIds.size }}/{{ pluginStore.plugins.length }})
          </label>
          <div class="export-actions">
            <button
              class="action-btn export-selected-btn"
              :disabled="selectedPluginIds.size === 0 || isPackaging"
              @click="handleExportSelected"
            >
              导出选中
            </button>
            <button
              class="action-btn export-all-btn"
              :disabled="pluginStore.plugins.length === 0 || isPackaging"
              @click="handleExportAll"
            >
              导出全部
            </button>
          </div>
        </div>

        <div v-if="pluginStore.plugins.length > 0" class="plugin-list">
          <div
            v-for="plugin in pluginStore.plugins"
            :key="plugin.id"
            class="plugin-item"
            :class="{ selected: selectedPluginIds.has(plugin.id) }"
            @click="togglePluginSelection(plugin.id)"
          >
            <input
              type="checkbox"
              :checked="selectedPluginIds.has(plugin.id)"
              @click.stop
              @change="togglePluginSelection(plugin.id)"
            />
            <div class="plugin-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
                <line x1="12" y1="22.08" x2="12" y2="12" />
              </svg>
            </div>
            <div class="plugin-info">
              <h4 class="plugin-name">{{ plugin.name }}</h4>
              <span class="plugin-version">v{{ plugin.version }}</span>
            </div>
            <span class="plugin-category">{{ plugin.category }}</span>
          </div>
        </div>

        <EmptyState
          v-else
          icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z' /><polyline points='3.27 6.96 12 12.01 20.73 6.96' /><line x1='12' y1='22.08' x2='12' y2='12' /></svg>"
          title="暂无已安装的插件"
          sub="导入插件后即可在此导出"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.plugin-import-export-page {
  padding: 24px;
  min-height: 100%;
  max-width: 1200px;
  margin: 0 auto;
}

.tabs-nav {
  display: flex;
  gap: 4px;
  margin-bottom: 20px;
  border-bottom: 2px solid var(--border-subtle);
}

.tab-btn {
  padding: 10px 20px;
  border: none;
  background: none;
  font-size: 14px;
  color: var(--fg-tertiary);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: color var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out);
  font-weight: 500;
  font-family: var(--font-sans-cn);
}

.tab-btn:hover {
  color: var(--fg-secondary);
}

.tab-btn.active {
  color: var(--gold-400);
  border-bottom-color: var(--gold-400);
}

.tab-content {
  padding: 24px;
}

.drop-zone {
  border: 2px dashed var(--border-default);
  border-radius: var(--radius-lg);
  padding: 48px 24px;
  text-align: center;
  cursor: pointer;
  transition: border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
  margin-bottom: 20px;
  color: var(--fg-tertiary);
}

.drop-zone:hover {
  border-color: var(--gold-400);
}

.drop-zone.dragging {
  border-color: var(--gold-400);
  background: var(--gold-tint);
}

.drop-zone.has-file {
  border-style: solid;
  border-color: var(--bronze-500);
  background: color-mix(in srgb, var(--bronze-500) 8%, transparent);
}

.drop-content .drop-icon {
  color: var(--gold-400);
  margin-bottom: 16px;
  display: flex;
  justify-content: center;
}

.drop-content h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 8px 0;
  font-family: var(--font-serif-cn);
}

.drop-content p {
  color: var(--fg-secondary);
  margin: 0 0 4px 0;
  font-size: 14px;
}

.format-hint {
  font-size: 12px;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
}

.file-info {
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: center;
}

.file-icon {
  color: var(--gold-400);
  display: flex;
}

.file-details {
  display: flex;
  flex-direction: column;
  text-align: left;
}

.file-name {
  font-weight: 600;
  color: var(--fg-primary);
}

.file-size {
  font-size: 12px;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
}

.remove-btn {
  width: 28px;
  height: 28px;
  border: none;
  background: var(--paper-tint);
  border-radius: var(--radius-sm);
  font-size: 18px;
  color: var(--fg-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background var(--dur-fast) var(--ease-out);
}

.remove-btn:hover {
  background: var(--paper-tint);
  opacity: 0.85;
}

.install-result {
  padding: 12px 16px;
  border-radius: var(--radius-md);
  margin-bottom: 20px;
  font-size: 14px;
}

.install-result.success {
  background: color-mix(in srgb, var(--bronze-500) 12%, transparent);
  color: var(--bronze-500);
  border: 1px solid color-mix(in srgb, var(--bronze-500) 30%, transparent);
}

.install-result.error {
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  color: var(--cinnabar-400);
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 30%, transparent);
}

.install-btn {
  width: 100%;
  padding: 14px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-lg);
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-instant) var(--ease-out);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  font-family: var(--font-sans-cn);
}

.install-btn:hover:not(:disabled) {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.install-btn:active:not(:disabled) {
  transform: translateY(1px);
}

.install-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.export-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 12px;
}

.select-all {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--fg-secondary);
  cursor: pointer;
}

.export-actions {
  display: flex;
  gap: 10px;
}

.action-btn {
  padding: 8px 20px;
  border: none;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-instant) var(--ease-out);
  font-family: var(--font-sans-cn);
}

.export-selected-btn {
  background: transparent;
  color: var(--fg-secondary);
  border: 1px solid var(--border-default);
}

.export-selected-btn:hover:not(:disabled) {
  background: var(--paper-tint);
  transform: translateY(-1px);
}

.export-all-btn {
  background: var(--gold-400);
  color: var(--ink-950);
}

.export-all-btn:hover:not(:disabled) {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.plugin-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 400px;
  overflow-y: auto;
}

.plugin-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}

.plugin-item:hover {
  border-color: var(--gold-400);
}

.plugin-item.selected {
  border-color: var(--gold-400);
  background: var(--gold-tint);
}

.plugin-icon {
  width: 40px;
  height: 40px;
  border-radius: var(--radius-md);
  background: var(--gold-tint);
  color: var(--gold-400);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.plugin-info {
  flex: 1;
  min-width: 0;
}

.plugin-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 2px 0;
}

.plugin-version {
  font-size: 12px;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
}

.plugin-category {
  background: var(--paper-tint);
  padding: 4px 10px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: var(--fg-secondary);
  flex-shrink: 0;
}

@media (max-width: 640px) {
  .plugin-import-export-page {
    padding: 16px;
  }

  .export-toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .export-actions {
    width: 100%;
  }

  .export-actions .action-btn {
    flex: 1;
  }
}
</style>
