<script setup lang="ts">
import { ref } from 'vue'
import { usePluginStore } from '@/stores/plugin'

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
    <header class="page-header">
      <h1 class="page-title">插件导入导出</h1>
      <p class="page-subtitle">导入新插件或导出已安装的插件</p>
    </header>

    <div class="tabs-nav">
      <button
        class="tab-btn"
        :class="{ active: activeTab === 'import' }"
        @click="activeTab = 'import'"
      >
        📥 导入插件
      </button>
      <button
        class="tab-btn"
        :class="{ active: activeTab === 'export' }"
        @click="activeTab = 'export'"
      >
        📤 导出插件
      </button>
    </div>

    <div class="tab-content">
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
            <div class="drop-icon">📦</div>
            <h3>拖拽插件包到此处</h3>
            <p>或点击选择文件</p>
            <p class="format-hint">支持 .forgeself-plugin 和 .zip 格式</p>
          </div>
          <div v-else class="file-info">
            <div class="file-icon">📄</div>
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

        <div class="plugin-list">
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
            <div class="plugin-icon">📦</div>
            <div class="plugin-info">
              <h4 class="plugin-name">{{ plugin.name }}</h4>
              <span class="plugin-version">v{{ plugin.version }}</span>
            </div>
            <span class="plugin-category">{{ plugin.category }}</span>
          </div>
        </div>

        <div v-if="pluginStore.plugins.length === 0" class="empty-state">
          暂无已安装的插件
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.plugin-import-export-page {
  padding: 24px;
  min-height: 100%;
}

.page-header {
  margin-bottom: 24px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
}

.page-subtitle {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.tabs-nav {
  display: flex;
  gap: 4px;
  margin-bottom: 24px;
  border-bottom: 2px solid var(--border-light);
}

.tab-btn {
  padding: 12px 24px;
  border: none;
  background: none;
  font-size: 15px;
  color: var(--text-muted);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 0.2s;
  font-weight: 500;
}

.tab-btn:hover {
  color: var(--text-secondary);
}

.tab-btn.active {
  color: var(--primary-color);
  border-bottom-color: var(--primary-color);
}

.tab-content {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 24px;
}

.drop-zone {
  border: 2px dashed var(--border-color);
  border-radius: 12px;
  padding: 48px 24px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 20px;
}

.drop-zone.dragging {
  border-color: var(--primary-color);
  background: var(--primary-soft);
}

.drop-zone.has-file {
  border-style: solid;
  border-color: var(--success-color);
  background: rgba(4, 120, 87, 0.06);
}

.drop-content .drop-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.drop-content h3 {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 8px 0;
}

.drop-content p {
  color: var(--text-muted);
  margin: 0 0 4px 0;
}

.format-hint {
  font-size: 12px;
  color: var(--text-muted);
}

.file-info {
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: center;
}

.file-icon {
  font-size: 32px;
}

.file-details {
  display: flex;
  flex-direction: column;
  text-align: left;
}

.file-name {
  font-weight: 600;
  color: var(--text-primary);
}

.file-size {
  font-size: 12px;
  color: var(--text-muted);
}

.remove-btn {
  width: 28px;
  height: 28px;
  border: none;
  background: var(--bg-muted);
  border-radius: 50%;
  font-size: 18px;
  color: var(--text-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.remove-btn:hover {
  background: var(--bg-hover);
}

.install-result {
  padding: 12px 16px;
  border-radius: 8px;
  margin-bottom: 20px;
  font-size: 14px;
}

.install-result.success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.install-result.error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.install-btn {
  width: 100%;
  padding: 14px;
  background: var(--primary-color);
  color: var(--primary-contrast);
  border: none;
  border-radius: 10px;
  font-size: 16px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}

.install-btn:hover:not(:disabled) {
  background: var(--primary-hover);
}

.install-btn:disabled {
  opacity: 0.6;
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
  color: var(--text-secondary);
  cursor: pointer;
}

.export-actions {
  display: flex;
  gap: 10px;
}

.action-btn {
  padding: 8px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.export-selected-btn {
  background: var(--bg-secondary);
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
}

.export-selected-btn:hover:not(:disabled) {
  background: var(--bg-hover);
}

.export-all-btn {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

.export-all-btn:hover:not(:disabled) {
  background: var(--primary-hover);
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
  border: 1px solid var(--border-color);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s;
}

.plugin-item:hover {
  border-color: var(--primary-color);
  background: var(--bg-secondary);
}

.plugin-item.selected {
  border-color: var(--primary-color);
  background: var(--primary-soft);
}

.plugin-icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: var(--primary-soft);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.plugin-info {
  flex: 1;
  min-width: 0;
}

.plugin-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 2px 0;
}

.plugin-version {
  font-size: 12px;
  color: var(--text-muted);
}

.plugin-category {
  background: var(--bg-muted);
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 12px;
  color: var(--text-secondary);
  flex-shrink: 0;
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
  color: var(--text-muted);
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