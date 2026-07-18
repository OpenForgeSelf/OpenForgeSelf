<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { PluginDetail } from '@/types/plugin'
import { PluginState, PluginPermission } from '@/types/plugin'
import { usePluginStore } from '@/stores/plugin'

const props = defineProps<{
  visible: boolean
  pluginId?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'toggle', plugin: PluginDetail): void
}>()

const pluginStore = usePluginStore()
const isToggling = ref(false)

const plugin = computed(() => pluginStore.currentPlugin)

const stateLabel = computed(() => {
  if (!plugin.value) return ''
  switch (plugin.value.state) {
    case PluginState.Running:
      return '已启用'
    case PluginState.Stopped:
      return '已禁用'
    case PluginState.Error:
      return '错误'
    case PluginState.Loaded:
      return '已安装'
    default:
      return '未知'
  }
})

const stateClass = computed(() => {
  return plugin.value ? `state-${plugin.value.state}` : ''
})

const permissionDescriptions: Record<PluginPermission, string> = {
  [PluginPermission.ReadSettings]: '读取插件设置',
  [PluginPermission.WriteSettings]: '修改插件设置',
  [PluginPermission.ReadData]: '读取应用数据',
  [PluginPermission.WriteData]: '写入应用数据',
  [PluginPermission.NetworkAccess]: '网络访问权限',
  [PluginPermission.FileSystem]: '文件系统访问',
  [PluginPermission.RegisterMenu]: '注册菜单项',
  [PluginPermission.RegisterTool]: '注册AI工具函数',
  [PluginPermission.RegisterRoute]: '注册路由页面'
}

const extensionTypeLabels: Record<string, string> = {
  menu: '菜单项',
  tool: 'AI工具函数',
  route: '路由页面',
  settings: '设置页面'
}

const formattedLastUsed = computed(() => {
  if (!plugin.value?.usageStats?.lastUsedAt) return '从未使用'
  const date = new Date(plugin.value.usageStats.lastUsedAt)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
})

async function loadDetail(): Promise<void> {
  if (props.pluginId) {
    await pluginStore.loadPluginDetail(props.pluginId)
  }
}

function handleClose(): void {
  emit('close')
}

async function handleToggle(): Promise<void> {
  if (!plugin.value || isToggling.value) return

  isToggling.value = true
  try {
    if (plugin.value.isEnabled) {
      await pluginStore.disablePlugin(plugin.value.id)
    } else {
      await pluginStore.enablePlugin(plugin.value.id)
    }
    if (plugin.value) {
      emit('toggle', plugin.value)
    }
  } catch (e) {
    console.error('切换插件状态失败:', e)
  } finally {
    isToggling.value = false
  }
}

function handleBackdropClick(event: MouseEvent): void {
  if (event.target === event.currentTarget) {
    handleClose()
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    handleClose()
  }
}

watch(() => props.visible, (newVal) => {
  if (newVal && props.pluginId) {
    loadDetail()
  }
})

watch(() => props.pluginId, () => {
  if (props.visible && props.pluginId) {
    loadDetail()
  }
})

onMounted(() => {
  if (props.visible && props.pluginId) {
    loadDetail()
  }
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="visible"
        class="modal-overlay"
        role="dialog"
        aria-modal="true"
        @click="handleBackdropClick"
      >
        <div class="modal-container" role="document">
          <div class="modal-header">
            <button class="close-btn" aria-label="关闭" @click="handleClose">
              ×
            </button>
          </div>

          <div v-if="pluginStore.isLoading" class="modal-loading">
            <div class="loading-spinner" />
            <span>加载中...</span>
          </div>

          <div v-else-if="pluginStore.error" class="modal-error">
            <span class="error-icon">⚠️</span>
            <p>{{ pluginStore.error }}</p>
          </div>

          <div v-else-if="plugin" class="modal-content">
            <div class="plugin-header">
              <div class="plugin-icon-large">
                <span v-if="plugin.iconUrl">
                  <img :src="plugin.iconUrl" :alt="plugin.name" />
                </span>
                <span v-else class="icon-fallback-large">📦</span>
              </div>
              <div class="plugin-info">
                <h2 class="plugin-name">{{ plugin.name }}</h2>
                <div class="plugin-meta-row">
                  <span class="plugin-version">版本 {{ plugin.version }}</span>
                  <span class="plugin-author">作者: {{ plugin.author }}</span>
                  <div class="plugin-state" :class="stateClass">
                    <span class="state-dot" />
                    <span class="state-text">{{ stateLabel }}</span>
                  </div>
                </div>
              </div>
            </div>

            <div class="plugin-description-full">
              <h3>插件描述</h3>
              <p>{{ plugin.description }}</p>
            </div>

            <div class="plugin-section">
              <h3>分类</h3>
              <span class="category-tag">{{ plugin.category }}</span>
            </div>

            <div v-if="plugin.dependencies && plugin.dependencies.length > 0" class="plugin-section">
              <h3>依赖插件</h3>
              <div class="dependencies-list">
                <span v-for="dep in plugin.dependencies" :key="dep" class="dependency-item">
                  {{ dep }}
                </span>
              </div>
            </div>

            <div v-if="plugin.permissions && plugin.permissions.length > 0" class="plugin-section">
              <h3>权限声明</h3>
              <ul class="permissions-list">
                <li v-for="perm in plugin.permissions" :key="perm" class="permission-item">
                  <span class="permission-icon">🔒</span>
                  <div class="permission-info">
                    <span class="permission-name">{{ permissionDescriptions[perm as PluginPermission] || perm }}</span>
                    <span class="permission-id">{{ perm }}</span>
                  </div>
                </li>
              </ul>
            </div>

            <div v-if="plugin.extensionPoints && plugin.extensionPoints.length > 0" class="plugin-section">
              <h3>扩展点贡献</h3>
              <div class="extensions-grid">
                <div v-for="ext in plugin.extensionPoints" :key="ext" class="extension-item">
                  <span class="extension-label">{{ extensionTypeLabels[ext] || ext }}</span>
                </div>
              </div>
            </div>

            <div v-if="plugin.usageStats" class="plugin-section">
              <h3>使用统计</h3>
              <div class="stats-grid">
                <div class="stat-item">
                  <span class="stat-value">{{ plugin.usageStats.totalUsage }}</span>
                  <span class="stat-label">总使用次数</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ formattedLastUsed }}</span>
                  <span class="stat-label">最后使用</span>
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button
                class="toggle-btn"
                :class="{ active: plugin.isEnabled, loading: isToggling }"
                :disabled="isToggling || plugin.state === PluginState.Error"
                @click="handleToggle"
              >
                <span v-if="isToggling" class="btn-loading" />
                {{ isToggling ? '处理中...' : (plugin.isEnabled ? '禁用插件' : '启用插件') }}
              </button>
            </div>
          </div>

          <div v-else class="modal-empty">
            <p>未找到插件信息</p>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
}

.modal-container {
  background: #fff;
  border-radius: 16px;
  width: 100%;
  max-width: 560px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.modal-header {
  display: flex;
  justify-content: flex-end;
  padding: 12px 16px 0;
  flex-shrink: 0;
}

.close-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: #f1f3f5;
  border-radius: 8px;
  font-size: 20px;
  color: #6c757d;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: #e9ecef;
  color: #212529;
}

.modal-content {
  padding: 0 24px 24px;
  overflow-y: auto;
  flex: 1;
}

.modal-loading,
.modal-error,
.modal-empty {
  padding: 60px 24px;
  text-align: center;
  color: #6c757d;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid #f1f3f5;
  border-top-color: #1976d2;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.error-icon {
  font-size: 48px;
  display: block;
  margin-bottom: 16px;
}

.modal-error p {
  margin: 0;
  color: #dc3545;
}

.plugin-header {
  display: flex;
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid #f1f3f5;
}

.plugin-icon-large {
  width: 64px;
  height: 64px;
  border-radius: 14px;
  background: #f0f7ff;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.plugin-icon-large img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.icon-fallback-large {
  font-size: 32px;
}

.plugin-info {
  flex: 1;
  min-width: 0;
}

.plugin-name {
  font-size: 20px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 8px 0;
}

.plugin-meta-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: #6c757d;
}

.plugin-version,
.plugin-author {
  color: #6c757d;
}

.plugin-state {
  display: flex;
  align-items: center;
  gap: 6px;
}

.state-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #6c757d;
}

.state-enabled .state-dot {
  background: #28a745;
}

.state-disabled .state-dot {
  background: #6c757d;
}

.state-error .state-dot {
  background: #dc3545;
}

.state-installed .state-dot {
  background: #17a2b8;
}

.state-text {
  color: #6c757d;
  font-size: 12px;
}

.state-enabled .state-text {
  color: #28a745;
}

.state-error .state-text {
  color: #dc3545;
}

.plugin-description-full {
  margin-bottom: 20px;
}

.plugin-description-full h3,
.plugin-section h3 {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 8px 0;
}

.plugin-description-full p {
  font-size: 14px;
  color: #495057;
  line-height: 1.6;
  margin: 0;
}

.plugin-section {
  margin-bottom: 20px;
}

.category-tag {
  display: inline-block;
  background: #f1f3f5;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  color: #495057;
}

.dependencies-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.dependency-item {
  background: #fff3cd;
  color: #856404;
  padding: 4px 10px;
  border-radius: 8px;
  font-size: 12px;
}

.permissions-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.permission-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  background: #f8f9fa;
  border-radius: 8px;
}

.permission-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.permission-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.permission-name {
  font-size: 13px;
  color: #212529;
  font-weight: 500;
}

.permission-id {
  font-size: 11px;
  color: #6c757d;
  font-family: monospace;
}

.extensions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 12px;
}

.extension-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 16px 8px;
  background: #f0f7ff;
  border-radius: 10px;
  text-align: center;
}

.extension-count {
  font-size: 24px;
  font-weight: 600;
  color: #1976d2;
  margin-bottom: 4px;
}

.extension-label {
  font-size: 12px;
  color: #495057;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 12px;
}

.stat-item {
  display: flex;
  flex-direction: column;
  padding: 16px;
  background: #f8f9fa;
  border-radius: 10px;
}

.stat-value {
  font-size: 20px;
  font-weight: 600;
  color: #212529;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 12px;
  color: #6c757d;
}

.modal-footer {
  padding: 16px 24px;
  border-top: 1px solid #f1f3f5;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  flex-shrink: 0;
}

.toggle-btn {
  padding: 10px 24px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 8px;
  background: #e9ecef;
  color: #495057;
}

.toggle-btn:hover:not(:disabled) {
  background: #dee2e6;
}

.toggle-btn.active {
  background: #dc3545;
  color: #fff;
}

.toggle-btn.active:hover:not(:disabled) {
  background: #c82333;
}

.toggle-btn:not(.active) {
  background: #28a745;
  color: #fff;
}

.toggle-btn:not(.active):hover:not(:disabled) {
  background: #218838;
}

.toggle-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.btn-loading {
  width: 16px;
  height: 16px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease;
}

.modal-enter-active .modal-container,
.modal-leave-active .modal-container {
  transition: transform 0.3s ease, opacity 0.3s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal-container,
.modal-leave-to .modal-container {
  transform: scale(0.95) translateY(-10px);
  opacity: 0;
}

@media (max-width: 640px) {
  .modal-overlay {
    padding: 0;
    align-items: stretch;
  }

  .modal-container {
    max-width: 100%;
    max-height: 100vh;
    border-radius: 0;
  }

  .modal-content {
    padding: 0 16px 16px;
  }

  .modal-footer {
    padding: 12px 16px;
  }

  .plugin-header {
    flex-direction: column;
    align-items: center;
    text-align: center;
  }

  .plugin-meta-row {
    justify-content: center;
  }
}
</style>
