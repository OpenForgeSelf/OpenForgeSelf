<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import { useOpenPage } from '@/composables/useOpenPage'
import type { PluginInfo, PluginCategory } from '@/types/plugin'
import { PluginState } from '@/types/plugin'

const { openPage } = useOpenPage()
const pluginStore = usePluginStore()

/** 状态徽标 CSS 类名：后端 state 是数字枚举（0-9），反查名后小写（running/stopped/error/notloaded…）。 */
function pluginStateClass(state: PluginState): string {
  return PluginState[state]?.toLowerCase() ?? 'unknown'
}

/** 状态徽标中文文案。 */
function pluginStateLabel(state: PluginState): string {
  switch (state) {
    case PluginState.NotLoaded:
      return '未加载'
    case PluginState.Loaded:
      return '已加载'
    case PluginState.Initialized:
      return '已初始化'
    case PluginState.Starting:
      return '启动中'
    case PluginState.Running:
      return '运行中'
    case PluginState.Stopping:
      return '停止中'
    case PluginState.Stopped:
      return '已停止'
    case PluginState.Destroying:
      return '销毁中'
    case PluginState.Destroyed:
      return '已销毁'
    case PluginState.Error:
      return '错误'
    default:
      return '未知'
  }
}

const activeTab = ref<'all' | 'recommended' | 'popular' | 'installed'>('all')
const searchKeyword = ref('')
const selectedCategory = ref<string>('all')
const sortBy = ref<'installs' | 'updated' | 'name'>('installs')
const debounceTimer = ref<number | null>(null)

const categories = computed<PluginCategory[]>(() => pluginStore.categories)

const sortOptions = [
  { value: 'installs', label: '安装量' },
  { value: 'updated', label: '更新时间' },
  { value: 'name', label: '名称' }
]

const filteredPlugins = computed(() => {
  let result = [...pluginStore.plugins]

  if (activeTab.value === 'installed') {
    result = result.filter(p => p.state !== PluginState.NotLoaded)
  }

  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.toLowerCase().trim()
    result = result.filter(p =>
      p.name.toLowerCase().includes(keyword) ||
      p.description.toLowerCase().includes(keyword) ||
      p.author.toLowerCase().includes(keyword) ||
      p.tags.some(t => t.toLowerCase().includes(keyword))
    )
  }

  if (selectedCategory.value !== 'all') {
    result = result.filter(p => p.category === selectedCategory.value)
  }

  switch (sortBy.value) {
    case 'installs':
      result.sort((a, b) => b.installCount - a.installCount)
      break
    case 'updated':
      result.sort((a, b) => {
        const aTime = a.updatedAt ? new Date(a.updatedAt).getTime() : 0
        const bTime = b.updatedAt ? new Date(b.updatedAt).getTime() : 0
        return bTime - aTime
      })
      break
    case 'name':
      result.sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))
      break
  }

  return result
})

function handleSearchInput(): void {
  if (debounceTimer.value) {
    clearTimeout(debounceTimer.value)
  }
  debounceTimer.value = window.setTimeout(() => {
    loadPlugins()
  }, 300)
}

function handleTabChange(tab: 'all' | 'recommended' | 'popular' | 'installed'): void {
  activeTab.value = tab
  if (tab === 'recommended') {
    pluginStore.loadRecommendedPlugins()
  } else if (tab === 'popular') {
    pluginStore.loadPopularPlugins()
  } else {
    loadPlugins()
  }
}

function handleCategoryChange(category: string): void {
  selectedCategory.value = category
}

function handleCardClick(plugin: PluginInfo): void {
  openPage(`/plugins/${plugin.id}`, plugin.name)
}

async function handleToggle(plugin: PluginInfo): Promise<void> {
  try {
    if (plugin.isEnabled) {
      await pluginStore.disablePlugin(plugin.id)
    } else {
      await pluginStore.enablePlugin(plugin.id)
    }
  } catch (e) {
    console.error('切换插件状态失败:', e)
  }
}

async function loadPlugins(): Promise<void> {
  const params: { keyword?: string; category?: string } = {}

  if (searchKeyword.value.trim()) {
    params.keyword = searchKeyword.value.trim()
  }
  if (selectedCategory.value !== 'all') {
    params.category = selectedCategory.value
  }

  await pluginStore.loadPlugins(params)
}

onMounted(() => {
  pluginStore.loadCategories()
  loadPlugins()
})
</script>

<template>
  <div class="plugin-store-page">
    <aside class="sidebar">
      <div class="sidebar-header">
        <h2 class="sidebar-title">插件市场</h2>
      </div>

      <div class="sidebar-section">
        <h3 class="section-title">分类</h3>
        <div class="category-list">
          <button
            class="category-item"
            :class="{ active: selectedCategory === 'all' }"
            @click="handleCategoryChange('all')"
          >
            <span class="category-icon">📦</span>
            <span class="category-name">全部插件</span>
            <span class="category-count">{{ pluginStore.plugins.length }}</span>
          </button>
          <button
            v-for="cat in categories"
            :key="cat.name"
            class="category-item"
            :class="{ active: selectedCategory === cat.name }"
            @click="handleCategoryChange(cat.name)"
          >
            <span class="category-icon">{{ cat.icon }}</span>
            <span class="category-name">{{ cat.name }}</span>
            <span class="category-count">{{ cat.count }}</span>
          </button>
        </div>
      </div>

      <div class="sidebar-section">
        <h3 class="section-title">管理</h3>
        <div class="nav-list">
          <router-link to="/plugins/updates" class="nav-item">
            <span class="nav-icon">🔄</span>
            <span class="nav-name">可更新</span>
            <span v-if="pluginStore.updates.length > 0" class="nav-badge">
              {{ pluginStore.updates.length }}
            </span>
          </router-link>
          <router-link to="/plugins/import-export" class="nav-item">
            <span class="nav-icon">📥</span>
            <span class="nav-name">导入导出</span>
          </router-link>
          <router-link to="/plugins/scaffolder" class="nav-item">
            <span class="nav-icon">🛠️</span>
            <span class="nav-name">开发脚手架</span>
          </router-link>
        </div>
      </div>
    </aside>

    <main class="main-content">
      <header class="content-header">
        <div class="header-top">
          <h1 class="page-title">
            {{ activeTab === 'recommended' ? '推荐插件' : 
              activeTab === 'popular' ? '热门插件' :
              activeTab === 'installed' ? '已安装插件' :
              selectedCategory === 'all' ? '全部插件' : selectedCategory }}
          </h1>
          <div class="sort-controls">
            <label class="sort-label">排序:</label>
            <select v-model="sortBy" class="sort-select">
              <option v-for="opt in sortOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </div>
        </div>

        <div class="tabs-bar">
          <button
            v-for="tab in [
              { value: 'all', label: '全部' },
              { value: 'recommended', label: '推荐' },
              { value: 'popular', label: '热门' },
              { value: 'installed', label: '已安装' }
            ]"
            :key="tab.value"
            class="tab-btn"
            :class="{ active: activeTab === tab.value }"
            @click="handleTabChange(tab.value as any)"
          >
            {{ tab.label }}
          </button>
        </div>

        <div class="search-bar">
          <span class="search-icon">🔍</span>
          <input
            v-model="searchKeyword"
            type="text"
            class="search-input"
            placeholder="搜索插件名称、描述、标签或作者..."
            @input="handleSearchInput"
          />
          <button
            v-if="searchKeyword"
            class="clear-btn"
            @click="searchKeyword = ''; loadPlugins()"
          >
            ×
          </button>
        </div>
      </header>

      <div class="results-info">
        <span v-if="!pluginStore.isLoading">
          共 {{ filteredPlugins.length }} 个插件
        </span>
      </div>

      <div v-if="pluginStore.isLoading" class="loading-state">
        <div class="loading-spinner" />
        <p>加载插件中...</p>
      </div>

      <div v-else-if="filteredPlugins.length === 0" class="empty-state">
        <div class="empty-icon">📭</div>
        <h3>没有找到插件</h3>
        <p v-if="searchKeyword || selectedCategory !== 'all'">
          试试调整搜索条件或筛选条件
        </p>
        <p v-else>暂无可用插件</p>
      </div>

      <div v-else class="plugins-grid">
        <div
          v-for="plugin in filteredPlugins"
          :key="plugin.id"
          class="plugin-card"
          @click="handleCardClick(plugin)"
        >
          <div class="card-header">
            <div class="plugin-icon">
              <span v-if="plugin.iconUrl">
                <img :src="plugin.iconUrl" :alt="plugin.name" />
              </span>
              <span v-else class="icon-fallback">📦</span>
            </div>
            <div class="plugin-status-badge" :class="pluginStateClass(plugin.state)">
              {{ pluginStateLabel(plugin.state) }}
            </div>
          </div>

          <div class="card-body">
            <h3 class="plugin-name">{{ plugin.name }}</h3>
            <p class="plugin-desc">{{ plugin.description }}</p>
          </div>

          <div class="card-meta">
            <span class="meta-item">
              <span class="meta-icon">👤</span>
              <span>{{ plugin.author }}</span>
            </span>
            <span class="meta-item">
              <span class="meta-icon">⬇️</span>
              <span>{{ plugin.installCount }}</span>
            </span>
            <span class="meta-item">
              <span class="meta-icon">⭐</span>
              <span>{{ plugin.rating.toFixed(1) }}</span>
            </span>
          </div>

          <div class="card-footer">
            <div class="footer-left">
              <span class="plugin-version">v{{ plugin.version }}</span>
              <span class="enabled-badge" :class="plugin.isEnabled ? 'on' : 'off'">
                {{ plugin.isEnabled ? '已启用' : '已停用' }}
              </span>
            </div>
            <button
              class="toggle-btn"
              :class="{ active: plugin.isEnabled }"
              :disabled="plugin.state === PluginState.Error"
              @click.stop="handleToggle(plugin)"
            >
              {{ plugin.isEnabled ? '禁用' : '启用' }}
            </button>
          </div>

          <div v-if="plugin.hasUpdate" class="update-badge">
            可更新
          </div>
        </div>
      </div>
    </main>
  </div>
</template>

<style scoped>
.plugin-store-page {
  display: flex;
  min-height: 100%;
}

.sidebar {
  width: 260px;
  flex-shrink: 0;
  background: var(--el-bg-color-page);
  border-right: 1px solid var(--el-border-color);
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.sidebar-header {
  padding: 20px 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.sidebar-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.sidebar-section {
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.section-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0 0 10px 0;
}

.category-list,
.nav-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.category-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: none;
  background: none;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  transition: all 150ms ease;
  text-align: left;
  width: 100%;
}

.category-item:hover {
  background: var(--el-fill-color);
}

.category-item.active {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.category-icon {
  font-size: 18px;
  flex-shrink: 0;
}

.category-name {
  flex: 1;
  font-size: 14px;
  font-weight: 500;
}

.category-count {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  padding: 2px 8px;
  border-radius: 10px;
}

.category-item.active .category-count {
  background: var(--primary-light);
  color: var(--el-color-primary);
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--el-border-radius-small);
  text-decoration: none;
  color: var(--el-text-color-regular);
  transition: all 150ms ease;
}

.nav-item:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.nav-item.router-link-active {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.nav-icon {
  font-size: 18px;
}

.nav-name {
  flex: 1;
  font-size: 14px;
  font-weight: 500;
}

.nav-badge {
  background: var(--el-color-danger);
  color: var(--el-color-white);
  font-size: 11px;
  font-weight: 600;
  padding: 2px 7px;
  border-radius: 10px;
  min-width: 18px;
  text-align: center;
}

.main-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
  min-width: 0;
}

.content-header {
  margin-bottom: 20px;
}

.header-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 12px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.sort-controls {
  display: flex;
  align-items: center;
  gap: 8px;
}

.sort-label {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.sort-select {
  padding: 6px 10px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-small);
  font-size: 13px;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
  cursor: pointer;
}

.tabs-bar {
  display: flex;
  gap: 4px;
  margin-bottom: 16px;
  border-bottom: 2px solid var(--el-border-color);
}

.tab-btn {
  padding: 10px 18px;
  border: none;
  background: none;
  font-size: 14px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 150ms ease;
  font-weight: 500;
}

.tab-btn:hover {
  color: var(--el-text-color-regular);
}

.tab-btn.active {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
}

.search-bar {
  position: relative;
  display: flex;
  align-items: center;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 10px;
  padding: 0 12px;
  transition: border-color 150ms ease, box-shadow 150ms ease;
}

.search-bar:focus-within {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.search-icon {
  font-size: 16px;
  margin-right: 10px;
}

.search-input {
  flex: 1;
  height: 40px;
  border: none;
  background: transparent;
  font-size: 14px;
  color: var(--el-text-color-primary);
}

.search-input::placeholder {
  color: var(--el-text-color-secondary);
}

.clear-btn {
  width: 22px;
  height: 22px;
  border: none;
  background: var(--el-fill-color-light);
  border-radius: 50%;
  font-size: 14px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.clear-btn:hover {
  background: var(--el-fill-color-light);
}

.results-info {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 16px;
}

.plugins-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.plugin-card {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 18px;
  cursor: pointer;
  transition: all 150ms ease;
  position: relative;
  display: flex;
  flex-direction: column;
}

.plugin-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  transform: translateY(-2px);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 14px;
}

.plugin-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background: var(--el-color-primary-light-9);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.plugin-icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.icon-fallback {
  font-size: 24px;
}

.plugin-status-badge {
  font-size: 11px;
  padding: 3px 8px;
  border-radius: 10px;
  font-weight: 500;
}

.plugin-status-badge.running {
  background: rgba(4, 120, 87, 0.15);
  color: var(--el-color-success);
}

.plugin-status-badge.stopped {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}

.plugin-status-badge.error {
  background: rgba(185, 28, 28, 0.15);
  color: var(--el-color-danger);
}

.plugin-status-badge.notloaded {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}

.card-body {
  flex: 1;
  margin-bottom: 14px;
}

.plugin-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 6px 0;
}

.plugin-desc {
  font-size: 13px;
  color: var(--el-text-color-regular);
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-meta {
  display: flex;
  gap: 14px;
  margin-bottom: 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.meta-icon {
  font-size: 12px;
}

.card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 14px;
  border-top: 1px solid var(--el-border-color);
}

.footer-left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.plugin-version {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  font-weight: 500;
}

.enabled-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 10px;
  font-weight: 500;
  white-space: nowrap;
}

.enabled-badge.on {
  background: rgba(4, 120, 87, 0.15);
  color: var(--el-color-success);
}

.enabled-badge.off {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}

.toggle-btn {
  padding: 6px 14px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  border-radius: var(--el-border-radius-small);
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-regular);
  cursor: pointer;
  transition: all 150ms ease;
}

.toggle-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.toggle-btn.active {
  background: var(--el-fill-color-light);
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}

.toggle-btn.active:hover {
  background: rgba(248, 81, 73, 0.08);
}

.toggle-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.update-badge {
  position: absolute;
  top: 12px;
  right: 12px;
  background: var(--el-color-warning);
  color: var(--el-color-white);
  font-size: 11px;
  font-weight: 600;
  padding: 3px 8px;
  border-radius: 4px;
}

.loading-state,
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
  border: 3px solid var(--el-border-color);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.loading-state p {
  color: var(--el-text-color-secondary);
  margin: 0;
}

.empty-icon {
  font-size: 56px;
  margin-bottom: 16px;
}

.empty-state h3 {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 8px 0;
}

.empty-state p {
  color: var(--el-text-color-secondary);
  margin: 0;
}

@media (max-width: 1024px) {
  .sidebar {
    width: 220px;
  }
}

@media (max-width: 768px) {
  .sidebar {
    display: none;
  }

  .main-content {
    padding: 16px;
  }

  .plugins-grid {
    grid-template-columns: 1fr;
  }
}
</style>
