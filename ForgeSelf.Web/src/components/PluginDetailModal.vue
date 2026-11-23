<script setup lang="ts">
import AppLogo from '@/components/AppLogo.vue'
import { ref, computed, watch } from 'vue'
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
  // 契约：class = state-<枚举名>（PascalCase，测试断言 state-Running 等）；数字枚举反查名正好匹配
  return plugin.value ? `state-${PluginState[plugin.value.state] ?? 'Unknown'}` : ''
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

function handleBackdropChange(value: boolean): void {
  if (!value) handleClose()
}

// immediate：visible=true 首挂即拉详情（旧版 onMounted 语义），后续开关由 watch 续接
watch(() => props.visible, (newVal) => {
  if (newVal && props.pluginId) {
    loadDetail()
  }
}, { immediate: true })

watch(() => props.pluginId, () => {
  if (props.visible && props.pluginId) {
    loadDetail()
  }
})
</script>

<template>
  <el-dialog
    :model-value="visible"
    :show-close="false"
    append-to-body
    class="plugin-detail-dialog"
    @update:model-value="handleBackdropChange"
    @close="handleClose"
  >
    <template #header>
      <div class="modal-header">
        <AppLogo :size="20" />
        <button class="close-btn" aria-label="关闭" @click="handleClose">
          ×
        </button>
      </div>
    </template>

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
  </el-dialog>
</template>

<style scoped>
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px 0;
  flex-shrink: 0;
}

.close-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: var(--el-fill-color-lighter);
  border-radius: 8px;
  font-size: 20px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

.modal-content {
  padding: 0 24px 24px;
}

.modal-loading,
.modal-error,
.modal-empty {
  padding: 60px 24px;
  text-align: center;
  color: var(--el-text-color-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--el-fill-color-lighter);
  border-top-color: var(--el-color-primary);
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
  color: var(--el-color-error);
}

.plugin-header {
  display: flex;
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--el-fill-color-lighter);
}

.plugin-icon-large {
  width: 64px;
  height: 64px;
  border-radius: 14px;
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
  bottom: 0;
  margin: 0 0 8px 0;
}

.plugin-meta-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.plugin-version,
.plugin-author {
  color: var(--el-text-color-secondary);
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
  background: var(--el-text-color-secondary);
}

.state-enabled .state-dot {
  background: var(--el-color-success);
}

.state-disabled .state-dot {
  background: var(--el-text-color-secondary);
}

.state-error .state-dot {
  background: var(--el-color-error);
}

.state-installed .state-dot {
  background: var(--el-color-info);
}

.state-text {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.state-enabled .state-text {
  color: var(--el-color-success);
}

.state-error .state-text {
  color: var(--el-color-error);
}

.plugin-description-full {
  margin-bottom: 20px;
}

.plugin-description-full h3,
.plugin-section h3 {
  font-size: 14px;
  font-weight: 600;
  background: var(--el-overlay-color);
  margin: 0 0 8px 0;
}

.plugin-description-full p {
  font-size: 14px;
  color: var(--el-text-color-regular);
  line-height: 1.6;
  margin: 0;
}

.plugin-section {
  margin-bottom: 20px;
}

.category-tag {
  display: inline-block;
  background: var(--el-fill-color-lighter);
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.dependencies-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.dependency-item {
  display: flex;
  align-items: center;
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
  justify-content: center;
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
  z-index: 1000;
  font-weight: 500;
}

.permission-id {
  font-size: 11px;
  color: var(--el-text-color-secondary);
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
  padding: 20px;
  border-radius: 10px;
  text-align: center;
}

.extension-count {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-color-primary);
  margin-bottom: 4px;
}

.extension-label {
  font-size: 12px;
  color: var(--el-text-color-regular);
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
  background: var(--el-bg-color);
  border-radius: 10px;
}

.stat-value {
  font-size: 20px;
  font-weight: 600;
  border-radius: 16px;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.modal-footer {
  padding: 16px 24px;
  border-top: 1px solid var(--el-fill-color-lighter);
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
  width: 100%;
  color: var(--el-text-color-regular);
}

.toggle-btn:hover:not(:disabled) {
  filter: brightness(1.05);
}

.toggle-btn.active {
  background: var(--el-color-error);
}

.toggle-btn.active:hover:not(:disabled) {
  background: var(--el-color-error-dark-2);
}

.toggle-btn:not(.active) {
  background: var(--el-color-success);
  display: flex;
}

.toggle-btn:not(.active):hover:not(:disabled) {
  background: var(--el-color-success-dark-2);
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

@media (max-width: 640px) {
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

<style>
/* el-dialog 壳层覆盖（append-to-body 后 dialog 根在组件子树外，须用全局选择器） */
.el-dialog.plugin-detail-dialog {
  --el-dialog-margin-top: 5vh;
  width: min(560px, calc(100vw - 32px));
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  border-radius: 16px;
  box-shadow: var(--el-box-shadow-dark);
  overflow: hidden;
}

.plugin-detail-dialog .el-dialog__header {
  padding: 0;
  margin-right: 0;
}

.plugin-detail-dialog .el-dialog__body {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

@media (max-width: 640px) {
  .el-dialog.plugin-detail-dialog {
    width: 100vw;
    max-height: 100vh;
    margin-top: 0;
    margin-bottom: 0;
    border-radius: 0;
  }
}
</style>
