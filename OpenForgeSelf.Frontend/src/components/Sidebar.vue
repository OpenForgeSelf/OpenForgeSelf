<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { usePluginStore } from '@/stores/plugin'
import { useThemeStore } from '@/stores/theme'
import type { ThemeMode } from '@/stores/theme'
import { setupPluginRoutes } from '@/router'
import type { PluginMenuItem } from '@/types/plugin'
import { getPluginRoutePrefix, generatePluginRouteName } from '@/router/pluginRoutes'

interface MenuTreeItem extends PluginMenuItem {
  children?: MenuTreeItem[]
}

const pluginStore = usePluginStore()
const themeStore = useThemeStore()
const router = useRouter()
const route = useRoute()

const isCollapsed = ref(false)
const expandedMenus = ref<Set<string>>(new Set())
const themeModes: Array<{ value: ThemeMode; label: string; shortLabel: string }> = [
  { value: 'light', label: '浅色模式', shortLabel: '浅' },
  { value: 'dark', label: '深色模式', shortLabel: '深' },
  { value: 'system', label: '跟随系统', shortLabel: '随' },
]

const menuTree = computed<MenuTreeItem[]>(() => {
  const items = pluginStore.sortedMenuItems
  const map = new Map<string, MenuTreeItem>()
  const roots: MenuTreeItem[] = []

  for (const item of items) {
    map.set(item.id, { ...item, children: [] })
  }

  for (const item of items) {
    const treeItem = map.get(item.id)!
    if (item.parentId && map.has(item.parentId)) {
      const parent = map.get(item.parentId)!
      if (!parent.children) {
        parent.children = []
      }
      parent.children.push(treeItem)
    } else {
      roots.push(treeItem)
    }
  }

  return roots
})

function getMenuPath(item: PluginMenuItem): string {
  return `${getPluginRoutePrefix()}/${item.path.replace(/^\//, '')}`
}

function isMenuActive(item: PluginMenuItem): boolean {
  const routeName = generatePluginRouteName(item.pluginId, item.path)
  return route.name === routeName
}

function toggleCollapse(): void {
  isCollapsed.value = !isCollapsed.value
}

function toggleSubmenu(itemId: string): void {
  if (expandedMenus.value.has(itemId)) {
    expandedMenus.value.delete(itemId)
  } else {
    expandedMenus.value.add(itemId)
  }
}

function isExpanded(itemId: string): boolean {
  return expandedMenus.value.has(itemId)
}

function hasChildren(item: MenuTreeItem): boolean {
  return !!(item.children && item.children.length > 0)
}

function setThemeMode(nextMode: ThemeMode): void {
  themeStore.setMode(nextMode)
}

function navigateTo(item: PluginMenuItem): void {
  if (hasChildren(item as MenuTreeItem)) {
    toggleSubmenu(item.id)
  } else {
    const path = getMenuPath(item)
    router.push(path)
  }
}

async function loadPluginMenu(): Promise<void> {
  await pluginStore.loadMenuItems()
  if (pluginStore.menuItems.length > 0) {
    setupPluginRoutes(pluginStore.menuItems)
  }
}

onMounted(() => {
  loadPluginMenu()
})
</script>

<template>
  <aside class="sidebar" :class="{ collapsed: isCollapsed }">
    <div class="sidebar-header">
      <h2 v-if="!isCollapsed">功能菜单</h2>
      <button class="collapse-btn" @click="toggleCollapse">
        {{ isCollapsed ? '›' : '‹' }}
      </button>
    </div>

    <nav class="sidebar-nav">
      <ul class="menu-list">
        <li class="menu-item">
          <router-link to="/" class="menu-link" :class="{ active: route.path === '/' }">
            <span class="menu-icon">💬</span>
            <span v-if="!isCollapsed" class="menu-text">聊天</span>
          </router-link>
        </li>

        <li class="menu-item">
          <router-link to="/memory" class="menu-link" :class="{ active: route.path === '/memory' }">
            <span class="menu-icon">🧠</span>
            <span v-if="!isCollapsed" class="menu-text">记忆管理</span>
          </router-link>
        </li>

        <li class="menu-item">
          <router-link to="/agents" class="menu-link" :class="{ active: route.path === '/agents' }">
            <span class="menu-icon">👥</span>
            <span v-if="!isCollapsed" class="menu-text">Agent 团队</span>
          </router-link>
        </li>

        <li class="menu-item">
          <router-link to="/profile" class="menu-link" :class="{ active: route.path === '/profile' }">
            <span class="menu-icon">🧬</span>
            <span v-if="!isCollapsed" class="menu-text">我的画像</span>
          </router-link>
        </li>

        <li class="menu-item">
          <router-link to="/plugins" class="menu-link" :class="{ active: route.path.startsWith('/plugins') }">
            <span class="menu-icon">🏪</span>
            <span v-if="!isCollapsed" class="menu-text">插件商店</span>
          </router-link>
        </li>

        <li v-for="item in menuTree" :key="item.id" class="menu-item">
          <div
            class="menu-link"
            :class="{
              active: isMenuActive(item),
              'has-children': hasChildren(item),
              expanded: isExpanded(item.id)
            }"
            @click="navigateTo(item)"
          >
            <span class="menu-icon">{{ item.icon || '📦' }}</span>
            <span v-if="!isCollapsed" class="menu-text">{{ item.name }}</span>
            <span v-if="hasChildren(item) && !isCollapsed" class="menu-arrow">
              {{ isExpanded(item.id) ? '▼' : '▶' }}
            </span>
          </div>

          <ul
            v-if="hasChildren(item) && isExpanded(item.id) && !isCollapsed"
            class="submenu-list"
          >
            <li v-for="child in item.children" :key="child.id" class="submenu-item">
              <router-link
                :to="getMenuPath(child)"
                class="submenu-link"
                :class="{ active: isMenuActive(child) }"
              >
                <span class="menu-icon">{{ child.icon || '📄' }}</span>
                <span class="menu-text">{{ child.name }}</span>
              </router-link>
            </li>
          </ul>
        </li>
      </ul>
    </nav>

    <div class="sidebar-footer">
      <div v-if="!isCollapsed" class="theme-switcher" role="group" aria-label="主题模式">
        <button
          v-for="theme in themeModes"
          :key="theme.value"
          type="button"
          class="theme-btn"
          :class="{ active: themeStore.mode === theme.value }"
          :title="theme.label"
          @click="setThemeMode(theme.value)"
        >
          {{ theme.shortLabel }}
        </button>
      </div>

      <span v-if="pluginStore.isLoading" class="sidebar-status">加载中...</span>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  width: 240px;
  height: 100vh;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--el-color-primary) 20%, transparent), transparent 18rem),
    var(--sidebar-bg);
  border-right: 1px solid var(--sidebar-border);
  box-shadow: 0 10px 15px rgba(0, 0, 0, 0.1);
  backdrop-filter: blur(18px);
  display: flex;
  flex-direction: column;
  transition:
    width 150ms ease,
    background-color 150ms ease,
    border-color 150ms ease;
  overflow: hidden;
}

.sidebar.collapsed {
  width: 60px;
}

.sidebar-header {
  padding: 20px 16px 16px;
  border-bottom: 1px solid var(--sidebar-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.sidebar-header h2 {
  font-size: 0.95rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin: 0;
  color: var(--el-text-color-primary);
}

.collapse-btn {
  width: 32px;
  height: 32px;
  border: 1px solid var(--el-border-color);
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  border-radius: var(--el-border-radius-base);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease,
    transform 150ms ease;
}

.collapse-btn:hover {
  background: var(--el-fill-color);
  border-color: var(--border-strong);
  color: var(--el-text-color-primary);
  transform: translateY(-1px);
}

.sidebar-nav {
  flex: 1;
  overflow-y: auto;
  padding: 12px 0;
}

.menu-list,
.submenu-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.menu-link,
.submenu-link {
  display: flex;
  align-items: center;
  margin: 0 10px;
  padding: 12px 14px;
  color: var(--el-text-color-regular);
  text-decoration: none;
  cursor: pointer;
  transition:
    background-color 150ms ease,
    color 150ms ease,
    border-color 150ms ease;
  gap: 12px;
  border: 1px solid transparent;
  border-radius: 12px;
}

.menu-link:hover,
.submenu-link:hover {
  background-color: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.menu-link.active,
.submenu-link.active {
  background:
    linear-gradient(135deg, var(--el-color-primary-light-9), transparent 80%),
    var(--el-fill-color-light);
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  box-shadow: inset 0 0 0 1px rgba(245, 158, 11, 0.08);
}

.menu-icon {
  font-size: 18px;
  width: 24px;
  text-align: center;
  flex-shrink: 0;
}

.menu-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.menu-arrow {
  font-size: 10px;
  color: var(--el-text-color-secondary);
}

.submenu-list {
  margin-top: 4px;
}

.submenu-link {
  margin-left: 22px;
  padding-left: 36px;
}

.sidebar-footer {
  padding: 14px 16px 18px;
  border-top: 1px solid var(--sidebar-border);
  font-size: 12px;
  color: var(--el-text-color-secondary);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.theme-switcher {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px;
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  background: var(--el-fill-color-light);
}

.theme-btn {
  min-width: 34px;
  height: 30px;
  border: none;
  border-radius: 12px;
  background: transparent;
  color: var(--el-text-color-regular);
  font-size: 12px;
  font-weight: 600;
  transition:
    background-color 150ms ease,
    color 150ms ease,
    transform 150ms ease;
}

.theme-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.theme-btn.active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.sidebar-status {
  white-space: nowrap;
}
</style>
