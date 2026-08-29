<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { usePluginStore } from '@/stores/plugin'
import { useOpenPage } from '@/composables/useOpenPage'
import { PluginState } from '@/types/plugin'

const route = useRoute()
const { openPage } = useOpenPage()
const pluginStore = usePluginStore()

const isToggling = ref(false)
const activeTab = ref('overview')

const pluginId = computed(() => route.params.id as string)
const plugin = computed(() => pluginStore.currentPlugin)

const stateLabel = computed(() => {
  if (!plugin.value) return ''
  switch (plugin.value.state) {
    case PluginState.Running:
      return '运行中'
    case PluginState.Stopped:
      return '已停止'
    case PluginState.Error:
      return '错误'
    case PluginState.NotLoaded:
      return '未加载'
    default:
      return '未知'
  }
})

const stateClass = computed(() => {
  return plugin.value ? `state-${plugin.value.state}` : ''
})

const formattedUpdatedAt = computed(() => {
  if (!plugin.value?.updatedAt) return '-'
  return new Date(plugin.value.updatedAt).toLocaleString('zh-CN')
})

async function handleToggle(): Promise<void> {
  if (!plugin.value || isToggling.value) return

  isToggling.value = true
  try {
    if (plugin.value.isEnabled) {
      await pluginStore.disablePlugin(plugin.value.id)
    } else {
      await pluginStore.enablePlugin(plugin.value.id)
    }
  } catch (e) {
    console.error('切换插件状态失败:', e)
  } finally {
    isToggling.value = false
  }
}

async function handleUninstall(): Promise<void> {
  if (!plugin.value) return
  if (!confirm(`确定要卸载插件 "${plugin.value.name}" 吗？`)) return

  try {
    await pluginStore.uninstallPlugin(plugin.value.id)
    openPage('/plugins', '插件商店')
  } catch (e) {
    console.error('卸载插件失败:', e)
  }
}

async function handleUpdate(): Promise<void> {
  if (!plugin.value) return
  try {
    await pluginStore.updatePlugin(plugin.value.id)
  } catch (e) {
    console.error('更新插件失败:', e)
  }
}

function handleBack(): void {
  openPage('/plugins', '插件商店')
}

onMounted(() => {
  if (pluginId.value) {
    pluginStore.loadPluginDetail(pluginId.value)
    pluginStore.loadPluginVersions(pluginId.value)
  }
})

watch(() => route.params.id, (newId) => {
  if (newId && typeof newId === 'string') {
    pluginStore.loadPluginDetail(newId)
    pluginStore.loadPluginVersions(newId)
  }
})
</script>

<template>
  <div class="plugin-detail-page">
    <div class="detail-header">
      <button class="back-btn" @click="handleBack">
        ← 返回
      </button>
    </div>

    <div v-if="pluginStore.isLoading && !plugin" class="loading-state">
      <div class="loading-spinner" />
      <p>加载插件详情中...</p>
    </div>

    <div v-else-if="pluginStore.error && !plugin" class="error-state">
      <div class="error-icon">⚠️</div>
      <p>{{ pluginStore.error }}</p>
    </div>

    <div v-else-if="plugin" class="detail-content">
      <div class="plugin-main-info">
        <div class="plugin-icon-large">
          <span v-if="plugin.iconUrl">
            <img :src="plugin.iconUrl" :alt="plugin.name" />
          </span>
          <span v-else class="icon-fallback">📦</span>
        </div>
        <div class="plugin-info">
          <h1 class="plugin-name">{{ plugin.name }}</h1>
          <div class="plugin-meta-row">
            <span class="plugin-version">v{{ plugin.version }}</span>
            <span class="plugin-author">{{ plugin.author }}</span>
            <div class="plugin-state" :class="stateClass">
              <span class="state-dot" />
              <span class="state-text">{{ stateLabel }}</span>
            </div>
          </div>
          <div class="plugin-stats">
            <span class="stat-item">
              <span class="stat-icon">⬇️</span>
              <span>{{ plugin.installCount }} 安装</span>
            </span>
            <span class="stat-item">
              <span class="stat-icon">⭐</span>
              <span>{{ plugin.rating.toFixed(1) }} 评分</span>
            </span>
            <span class="stat-item">
              <span class="stat-icon">🕐</span>
              <span>更新于 {{ formattedUpdatedAt }}</span>
            </span>
          </div>
        </div>
        <div class="plugin-actions">
          <button
            class="action-btn toggle-btn"
            :class="{ active: plugin.isEnabled, loading: isToggling }"
            :disabled="isToggling || plugin.state === 'Error'"
            @click="handleToggle"
          >
            {{ plugin.isEnabled ? '禁用' : '启用' }}
          </button>
          <button
            v-if="plugin.hasUpdate"
            class="action-btn update-btn"
            @click="handleUpdate"
          >
            更新到 v{{ plugin.latestVersion }}
          </button>
          <button class="action-btn uninstall-btn" @click="handleUninstall">
            卸载
          </button>
        </div>
      </div>

      <div class="tabs-nav">
        <button
          v-for="tab in [
            { key: 'overview', label: '概览' },
            { key: 'versions', label: '版本历史' },
            { key: 'screenshots', label: '截图' },
            { key: 'dependencies', label: '依赖和权限' }
          ]"
          :key="tab.key"
          class="tab-btn"
          :class="{ active: activeTab === tab.key }"
          @click="activeTab = tab.key"
        >
          {{ tab.label }}
        </button>
      </div>

      <div class="tab-content">
        <div v-if="activeTab === 'overview'" class="tab-panel">
          <div class="section">
            <h3>插件描述</h3>
            <p class="description-text">{{ plugin.description }}</p>
          </div>

          <div class="section">
            <h3>分类与标签</h3>
            <div class="tags-row">
              <span class="category-tag">{{ plugin.category }}</span>
              <span v-for="tag in plugin.tags" :key="tag" class="tag-item">
                {{ tag }}
              </span>
            </div>
          </div>

          <div v-if="plugin.releaseNotes" class="section">
            <h3>更新日志</h3>
            <div class="release-notes">
              <pre>{{ plugin.releaseNotes }}</pre>
            </div>
          </div>

          <div v-if="plugin.homepageUrl || plugin.repositoryUrl" class="section">
            <h3>链接</h3>
            <div class="links-row">
              <a v-if="plugin.homepageUrl" :href="plugin.homepageUrl" target="_blank" class="link-btn">
                🌐 主页
              </a>
              <a v-if="plugin.repositoryUrl" :href="plugin.repositoryUrl" target="_blank" class="link-btn">
                💻 代码仓库
              </a>
            </div>
          </div>

          <div v-if="plugin.license" class="section">
            <h3>许可证</h3>
            <span class="license-badge">{{ plugin.license }}</span>
          </div>
        </div>

        <div v-else-if="activeTab === 'versions'" class="tab-panel">
          <div class="section">
            <h3>版本历史</h3>
            <div v-if="pluginStore.versions.length === 0" class="empty-state">
              暂无版本历史记录
            </div>
            <div v-else class="versions-list">
              <div
                v-for="(ver, index) in pluginStore.versions"
                :key="ver.version"
                class="version-item"
                :class="{ latest: index === 0 }"
              >
                <div class="version-header">
                  <span class="version-number">v{{ ver.version }}</span>
                  <span v-if="index === 0" class="latest-badge">当前版本</span>
                  <span v-if="ver.releasedAt" class="version-date">
                    {{ new Date(ver.releasedAt).toLocaleDateString('zh-CN') }}
                  </span>
                </div>
                <div v-if="ver.releaseNotes" class="version-notes">
                  {{ ver.releaseNotes }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-else-if="activeTab === 'screenshots'" class="tab-panel">
          <div class="section">
            <h3>截图预览</h3>
            <div v-if="plugin.screenshots.length === 0" class="empty-state">
              暂无截图
            </div>
            <div v-else class="screenshots-grid">
              <div v-for="(src, index) in plugin.screenshots" :key="index" class="screenshot-item">
                <img :src="src" :alt="`截图 ${index + 1}`" />
              </div>
            </div>
          </div>
        </div>

        <div v-else-if="activeTab === 'dependencies'" class="tab-panel">
          <div class="section">
            <h3>依赖插件</h3>
            <div v-if="plugin.dependencies.length === 0" class="empty-state">
              无依赖插件
            </div>
            <div v-else class="dependencies-list">
              <span v-for="dep in plugin.dependencies" :key="dep" class="dependency-item">
                {{ dep }}
              </span>
            </div>
          </div>

          <div class="section">
            <h3>权限声明</h3>
            <div v-if="plugin.permissions.length === 0" class="empty-state">
              无特殊权限要求
            </div>
            <ul v-else class="permissions-list">
              <li v-for="perm in plugin.permissions" :key="perm" class="permission-item">
                <span class="permission-icon">🔒</span>
                <span class="permission-name">{{ perm }}</span>
              </li>
            </ul>
          </div>

          <div class="section">
            <h3>扩展点</h3>
            <div v-if="plugin.extensionPoints.length === 0" class="empty-state">
              无扩展点贡献
            </div>
            <div v-else class="extensions-list">
              <span v-for="ep in plugin.extensionPoints" :key="ep" class="extension-item">
                {{ ep }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.plugin-detail-page {
  padding: 24px;
  min-height: 100%;
}

.detail-header {
  margin-bottom: 16px;
}

.back-btn {
  background: none;
  border: none;
  color: var(--el-color-primary);
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: var(--el-border-radius-small);
  transition: background 150ms ease;
}

.back-btn:hover {
  background: var(--el-color-primary-light-9);
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
  color: var(--el-text-color-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--el-border-color);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.detail-content {
  background: var(--el-bg-color);
  border-radius: 12px;
  border: 1px solid var(--el-border-color);
  overflow: hidden;
}

.plugin-main-info {
  display: flex;
  gap: 24px;
  padding: 24px;
  border-bottom: 1px solid var(--el-border-color);
  flex-wrap: wrap;
}

.plugin-icon-large {
  width: 80px;
  height: 80px;
  border-radius: 12px;
  background: var(--el-color-primary-light-9);
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

.icon-fallback {
  font-size: 40px;
}

.plugin-info {
  flex: 1;
  min-width: 0;
}

.plugin-name {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 8px 0;
}

.plugin-meta-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 12px;
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

.state-Running .state-dot { background: var(--el-color-success); }
.state-Stopped .state-dot { background: var(--el-text-color-secondary); }
.state-Error .state-dot { background: var(--el-color-danger); }
.state-Running .state-text { color: var(--el-color-success); }
.state-Error .state-text { color: var(--el-color-danger); }

.plugin-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.stat-icon {
  font-size: 14px;
}

.plugin-actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex-shrink: 0;
}

.action-btn {
  padding: 10px 20px;
  border: none;
  border-radius: var(--el-border-radius-small);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 150ms ease;
  white-space: nowrap;
}

.toggle-btn {
  background: var(--el-color-success);
  color: var(--el-color-white);
}

.toggle-btn.active {
  background: var(--el-color-danger);
}

.toggle-btn:hover:not(:disabled) {
  opacity: 0.9;
}

.update-btn {
  background: var(--el-color-warning);
  color: var(--el-color-white);
}

.update-btn:hover {
  opacity: 0.9;
}

.uninstall-btn {
  background: var(--el-fill-color-light);
  color: var(--el-color-danger);
  border: 1px solid var(--el-border-color);
}

.uninstall-btn:hover {
  background: var(--el-fill-color);
}

.tabs-nav {
  display: flex;
  gap: 4px;
  padding: 0 24px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-fill-color-light);
}

.tab-btn {
  padding: 12px 20px;
  border: none;
  background: none;
  font-size: 14px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
  transition: all 150ms ease;
}

.tab-btn:hover {
  color: var(--el-text-color-regular);
}

.tab-btn.active {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
  font-weight: 500;
}

.tab-content {
  padding: 24px;
}

.section {
  margin-bottom: 24px;
}

.section:last-child {
  margin-bottom: 0;
}

.section h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.description-text {
  font-size: 14px;
  color: var(--el-text-color-regular);
  line-height: 1.7;
  margin: 0;
}

.tags-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.category-tag {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}

.tag-item {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  padding: 4px 10px;
  border-radius: 10px;
  font-size: 12px;
}

.release-notes pre {
  margin: 0;
  padding: 16px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-small);
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-wrap: break-word;
  color: var(--el-text-color-regular);
  font-family: inherit;
}

.links-row {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.link-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-small);
  text-decoration: none;
  color: var(--el-text-color-regular);
  font-size: 13px;
  transition: all 150ms ease;
}

.link-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.license-badge {
  display: inline-block;
  padding: 6px 14px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  border-radius: var(--el-border-radius-small);
  font-size: 13px;
  font-weight: 500;
}

.versions-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.version-item {
  padding: 16px;
  background: var(--el-fill-color-light);
  border-radius: 10px;
  border-left: 3px solid var(--el-border-color);
}

.version-item.latest {
  border-left-color: var(--el-color-success);
  background: rgba(4, 120, 87, 0.08);
}

.version-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}

.version-number {
  font-weight: 600;
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.latest-badge {
  background: var(--el-color-success);
  color: var(--el-color-white);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
}

.version-date {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.version-notes {
  font-size: 13px;
  color: var(--el-text-color-regular);
  line-height: 1.6;
}

.screenshots-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.screenshot-item {
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid var(--el-border-color);
}

.screenshot-item img {
  width: 100%;
  display: block;
}

.dependencies-list,
.extensions-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.dependency-item {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  padding: 6px 12px;
  border-radius: var(--el-border-radius-small);
  font-size: 12px;
}

.extension-item {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  padding: 6px 12px;
  border-radius: var(--el-border-radius-small);
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
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-small);
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.permission-icon {
  font-size: 14px;
}

@media (max-width: 768px) {
  .plugin-detail-page {
    padding: 16px;
  }

  .plugin-main-info {
    flex-direction: column;
    align-items: flex-start;
  }

  .plugin-actions {
    width: 100%;
    flex-direction: row;
  }

  .plugin-actions .action-btn {
    flex: 1;
  }

  .tabs-nav {
    overflow-x: auto;
    padding: 0 16px;
  }

  .tab-btn {
    flex-shrink: 0;
  }
}
</style>
